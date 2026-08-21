using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Academy.Application.Assessments;
using Academy.Application.Auth;
using Academy.Application.Billing;
using Academy.Application.Programs;
using Academy.Domain;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Assessments;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>
/// INVERTA M4 acceptance (KAK §16.3): server-authoritative timers, proctoring strikes and
/// reinstatement, ITP score conversion that fails loudly, and immutable score-based certificates.
/// </summary>
public class FinalAssessmentTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    // ---- timers are server-authoritative (GR-12) ----

    [Fact]
    public async Task Sections_run_sequentially_with_server_computed_deadlines()
    {
        var c = await SetUp();
        var state = await Start(c);

        Assert.Equal("InProgress", state.Status);
        Assert.Equal(0, state.SectionIndex);
        Assert.Equal(3, state.SectionCount);
        Assert.Equal(nameof(QuestionSection.Listening), state.CurrentSection);
        Assert.NotNull(state.SectionDeadline);
        Assert.True(state.SecondsRemaining > 0);
        // Only the ACTIVE section's questions are ever served.
        Assert.All(state.Questions, q => Assert.Equal(nameof(QuestionSection.Listening), q.Section));
    }

    [Fact]
    public async Task Advancing_closes_a_section_and_it_cannot_be_returned_to()
    {
        var c = await SetUp();
        var state = await Start(c);
        var listeningQ = state.Questions[0].Id.ToString();

        var afterAdvance = await Advance(c, state.AttemptId, null);
        Assert.Equal(1, afterAdvance.SectionIndex);
        Assert.Equal(nameof(QuestionSection.Structure), afterAdvance.CurrentSection);

        // Writing to the now-closed Listening section is silently ignored — it is final.
        var after = await SaveAnswers(c, state.AttemptId, new Dictionary<string, int> { [listeningQ] = 0 });
        Assert.DoesNotContain(listeningQ, after.Answers.Keys);
    }

    [Fact]
    public async Task An_expired_section_is_auto_submitted_and_scored_as_of_expiry()
    {
        var c = await SetUp();
        var state = await Start(c);

        // Answer the first section correctly, then rewind every section clock past its deadline.
        await SaveAnswers(c, state.AttemptId, CorrectFor(c, QuestionSection.Listening));
        await ExpireAllSectionsAsync(state.AttemptId);

        // Any subsequent call must apply the elapsed time: the sitting is over.
        var result = await Finish(c, state.AttemptId);

        Assert.True(result.AutoSubmitted);
        // Answers saved before expiry still count — scored as-of expiry, not discarded.
        Assert.Equal(ToeflScoring.ListeningQuestions, result.SectionScores[nameof(QuestionSection.Listening)]);
        Assert.Equal(0, result.SectionScores[nameof(QuestionSection.Structure)]);
    }

    [Fact]
    public async Task Answers_are_rejected_after_the_attempt_is_submitted()
    {
        var c = await SetUp();
        var state = await Start(c);
        await FinishAllSections(c, state.AttemptId);

        var res = await Authed(HttpMethod.Post, $"/api/attempts/{state.AttemptId}/section-answers", c.Token,
            new { answers = new Dictionary<string, int>() });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // ---- proctoring (GR-13) ----

    [Fact]
    public async Task Focus_loss_under_two_seconds_is_recorded_but_never_a_strike()
    {
        var c = await SetUp();
        var state = await Start(c);

        for (var i = 0; i < 5; i++)
        {
            var p = await Proctor(c, state.AttemptId, "WindowBlur", durationMs: 500);
            Assert.Equal(0, p.Strikes);
            Assert.Equal("none", p.Action);
        }

        // Ignored events are still persisted for the dispute trail.
        var events = await WithDbResult(db => db.ProctorEvents.CountAsync(e => e.AttemptId == state.AttemptId));
        Assert.Equal(5, events);
        Assert.False((await GetState(c, state.AttemptId)).ProctorFlagged);
    }

    [Fact]
    public async Task Two_strikes_auto_submit_and_flag_the_attempt()
    {
        var c = await SetUp();
        var state = await Start(c);

        var first = await Proctor(c, state.AttemptId, "WindowBlur", durationMs: 5000);
        Assert.Equal(1, first.Strikes);
        Assert.Equal("warn", first.Action);
        Assert.False(first.Flagged);

        var second = await Proctor(c, state.AttemptId, "VisibilityHidden", durationMs: 9000);
        Assert.Equal(2, second.Strikes);
        Assert.Equal("autoSubmit", second.Action);
        Assert.True(second.Flagged);

        var after = await GetState(c, state.AttemptId);
        Assert.Equal("Submitted", after.Status);
        Assert.True(after.ProctorFlagged);
    }

    [Fact]
    public async Task Reinstating_clears_the_flag_but_keeps_the_event_trail()
    {
        var c = await SetUp();
        var state = await Start(c);
        await Proctor(c, state.AttemptId, "WindowBlur", 5000);
        await Proctor(c, state.AttemptId, "WindowBlur", 5000);

        var before = await WithDbResult(db => db.ProctorEvents.CountAsync(e => e.AttemptId == state.AttemptId));
        Assert.True(before > 0);

        (await Authed(HttpMethod.Post, $"/api/admin/attempts/{state.AttemptId}/reinstate", c.Admin))
            .EnsureSuccessStatusCode();

        await WithDb(async db =>
        {
            var a = await db.Attempts.FirstAsync(x => x.Id == state.AttemptId);
            Assert.False(a.ProctorFlagged);
            Assert.True(a.Reinstated);
            // The trail must survive — it is the evidence for the dispute.
            Assert.Equal(before, await db.ProctorEvents.CountAsync(e => e.AttemptId == state.AttemptId));
        });
    }

    // ---- scoring & certificate (GR-6, KAK §9.9.4) ----

    [Fact]
    public async Task A_perfect_sitting_converts_to_the_ITP_total_and_issues_a_certificate()
    {
        var c = await SetUp();
        var state = await Start(c);
        var result = await AnswerEverythingCorrectly(c, state.AttemptId);

        Assert.Equal(ToeflScoring.TotalQuestions, result.Score);
        Assert.NotNull(result.TotalScaledScore);
        Assert.True(ToeflScoring.IsValidTotal(result.TotalScaledScore!.Value));

        var certs = await AuthedGet<List<ProgramCertificateDto>>("/api/me/program-certificates", c.Token);
        var cert = Assert.Single(certs);
        Assert.Equal(result.TotalScaledScore, cert.TotalScore);
        Assert.StartsWith("INV-", cert.VerificationCode);
        Assert.True(factory.Email.CertificateEmailCount >= 1);
    }

    [Fact]
    public async Task Certificate_verifies_publicly_and_always_carries_the_prediction_disclaimer()
    {
        var c = await SetUp();
        var state = await Start(c);
        await AnswerEverythingCorrectly(c, state.AttemptId);
        var cert = (await AuthedGet<List<ProgramCertificateDto>>("/api/me/program-certificates", c.Token)).First();

        var verify = await _client.GetFromJsonAsync<CertificateVerificationDto>(
            $"/api/program-certificates/verify/{cert.VerificationCode}", Json);

        Assert.True(verify!.Valid);
        Assert.Equal(cert.TotalScore, verify.TotalScore);
        Assert.NotNull(verify.SectionScores);
        Assert.Contains("BUKAN skor TOEFL resmi", verify.Disclaimer);

        // An unknown code is valid:false with the disclaimer — never an error page.
        var bogus = await _client.GetFromJsonAsync<CertificateVerificationDto>(
            "/api/program-certificates/verify/INV-NOPENOPE", Json);
        Assert.False(bogus!.Valid);
        Assert.Contains("BUKAN skor TOEFL resmi", bogus.Disclaimer);
    }

    [Fact]
    public async Task Certificate_pdf_renders_with_embedded_fonts()
    {
        var c = await SetUp();
        var state = await Start(c);
        await AnswerEverythingCorrectly(c, state.AttemptId);
        var cert = (await AuthedGet<List<ProgramCertificateDto>>("/api/me/program-certificates", c.Token)).First();

        var res = await Authed(HttpMethod.Get, $"/api/program-certificates/{cert.Id}/pdf", c.Token);
        res.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", res.Content.Headers.ContentType?.MediaType);
        var bytes = await res.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 1000);
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }

    [Fact]
    public async Task An_unmapped_score_fails_loudly_and_issues_no_certificate()
    {
        // Score bands deliberately cover only raw 0 — any higher score is unmapped.
        var c = await SetUp(bandsCoverOnlyZero: true);
        var state = await Start(c);
        var result = await AnswerEverythingCorrectly(c, state.AttemptId);

        // The attempt is preserved and scored...
        Assert.Equal(ToeflScoring.TotalQuestions, result.Score);
        // ...but no band is guessed and NO certificate is issued (KAK §9.9.4).
        Assert.Null(result.TotalScaledScore);
        Assert.Null(result.PredictedBand);
        Assert.Empty(await AuthedGet<List<ProgramCertificateDto>>("/api/me/program-certificates", c.Token));
    }

    [Fact]
    public async Task A_retake_issues_a_NEW_certificate_and_leaves_the_first_untouched()
    {
        var c = await SetUp(retakeCap: 2);
        var first = await Start(c);
        await AnswerEverythingCorrectly(c, first.AttemptId);

        var certs1 = await AuthedGet<List<ProgramCertificateDto>>("/api/me/program-certificates", c.Token);
        var original = Assert.Single(certs1);

        // A second sitting is a different attempt ⇒ a second, independent certificate (GR-6).
        var second = await Start(c);
        Assert.NotEqual(first.AttemptId, second.AttemptId);
        await AnswerEverythingCorrectly(c, second.AttemptId);

        var certs2 = await AuthedGet<List<ProgramCertificateDto>>("/api/me/program-certificates", c.Token);
        Assert.Equal(2, certs2.Count);

        var stillThere = certs2.Single(x => x.Id == original.Id);
        Assert.Equal(original.VerificationCode, stillThere.VerificationCode);
        Assert.Equal(original.IssuedAt, stillThere.IssuedAt);
        Assert.Equal(original.TotalScore, stillThere.TotalScore);
        // Both codes still verify — the earlier certificate is never invalidated.
        Assert.True((await _client.GetFromJsonAsync<CertificateVerificationDto>(
            $"/api/program-certificates/verify/{original.VerificationCode}", Json))!.Valid);
    }

    [Fact]
    public async Task The_default_final_assessment_allows_only_one_attempt()
    {
        var c = await SetUp();                       // retakeCap defaults to 1
        var state = await Start(c);
        await AnswerEverythingCorrectly(c, state.AttemptId);

        var res = await Authed(HttpMethod.Post, $"/api/assessments/{c.AssessmentId}/attempts", c.Token);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Submitting_the_final_assessment_completes_its_session()
    {
        var c = await SetUp();
        var state = await Start(c);
        var result = await AnswerEverythingCorrectly(c, state.AttemptId);
        Assert.True(result.SessionCompleted);
    }

    // ---- audio play limit ----

    [Fact]
    public async Task Listening_audio_play_limit_is_enforced_server_side()
    {
        var c = await SetUp(audioPlayLimit: 1);
        var state = await Start(c);
        var audioQ = state.Questions.First(q => q.HasAudio).Id;

        var first = await Authed(HttpMethod.Get, $"/api/attempts/{state.AttemptId}/audio/{audioQ}", c.Token);
        first.EnsureSuccessStatusCode();

        var second = await Authed(HttpMethod.Get, $"/api/attempts/{state.AttemptId}/audio/{audioQ}", c.Token);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // ---- ownership & gating ----

    [Fact]
    public async Task Another_user_cannot_read_or_drive_the_sitting()
    {
        var c = await SetUp();
        var state = await Start(c);
        var (otherToken, _) = await VerifiedUser();

        Assert.Equal(HttpStatusCode.NotFound,
            (await Authed(HttpMethod.Get, $"/api/attempts/{state.AttemptId}/state", otherToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Authed(HttpMethod.Post, $"/api/attempts/{state.AttemptId}/finish", otherToken)).StatusCode);
    }

    // ================================================================ helpers

    private record Ctx(string Token, Guid UserId, string Admin, Guid ProgramId, Guid SessionId,
                       Guid AssessmentId,
                       IReadOnlyDictionary<QuestionSection, List<(Guid Id, int Correct)>> Key);

    /// <summary>
    /// Builds an isolated program whose ONLY session is a 3-section final assessment, enrolls a
    /// learner, and seeds a complete score-conversion table.
    /// </summary>
    private async Task<Ctx> SetUp(int? retakeCap = 1, int? audioPlayLimit = null, bool bandsCoverOnlyZero = false)
    {
        var admin = await AdminToken();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var program = await PostJson<AdminProgramDto>("/api/admin/programs", admin, new
        {
            name = $"Final {suffix}", slug = $"final-{suffix}", description = "Uji M4",
            summary = (string?)null, priceIdr = 100000m, published = false,
        });

        // A full ITP-format sitting (50/40/50) — readiness refuses to publish anything smaller,
        // and this file's assertions are about the sitting, not about question authoring.
        var assessment = await ItpFinal.SeedAsync(factory, retakeCap: retakeCap, audioPlayLimit: audioPlayLimit);
        var key = assessment.Key;

        var session = await PostJson<AdminSessionDto>($"/api/admin/programs/{program.Id}/sessions", admin, new
        {
            type = "FinalAssessment", title = "Tes Akhir", description = (string?)null, orderIndex = 1,
            providerAssetId = (string?)null, durationSeconds = (int?)null,
            scheduledAt = (DateTimeOffset?)null, liveMode = (string?)null,
            joinUrl = (string?)null, location = (string?)null, assessmentId = assessment.AssessmentId,
        });

        // Score bands must cover the FULL ITP raw-score range per section — full coverage so
        // the program is publishable...
        var sectionSizes = new (string Section, int MaxRaw, int ScaledMax)[]
        {
            ("Listening", ToeflScoring.ListeningQuestions, ToeflScoring.ListeningScaledMax),
            ("Structure", ToeflScoring.StructureQuestions, ToeflScoring.StructureScaledMax),
            ("Reading",   ToeflScoring.ReadingQuestions,   ToeflScoring.ReadingScaledMax),
        };
        var rows = new List<(string Section, int Raw, int MaxRaw, int ScaledMax)>();
        foreach (var (section, maxRaw, scaledMax) in sectionSizes)
            for (var raw = 0; raw <= maxRaw; raw++)
                rows.Add((section, raw, maxRaw, scaledMax));
        object BandRow((string Section, int Raw, int MaxRaw, int ScaledMax) r) => new
        {
            id = Guid.Empty, section = r.Section, minRaw = r.Raw, maxRaw = r.Raw,
            scaledScore = ToeflScoring.ScaledMin
                + (int)Math.Round((double)r.Raw / r.MaxRaw * (r.ScaledMax - ToeflScoring.ScaledMin)),
            predictedBand = (string?)null,
        };
        (await Authed(HttpMethod.Put, $"/api/admin/programs/{program.Id}/score-bands", admin,
            new { bands = rows.Select(BandRow) })).EnsureSuccessStatusCode();

        (await Authed(HttpMethod.Put, $"/api/admin/programs/{program.Id}", admin, new
        {
            name = program.Name, slug = program.Slug, description = program.Description,
            summary = program.Summary, priceIdr = program.PriceIdr, published = true,
        })).EnsureSuccessStatusCode();

        // ...then, for the fail-loudly test, drift the table down to only raw 0 post-publish.
        // Publish gates on readiness; it never re-checks bands an admin edits afterwards.
        if (bandsCoverOnlyZero)
            (await Authed(HttpMethod.Put, $"/api/admin/programs/{program.Id}/score-bands", admin,
                new { bands = rows.Where(r => r.Raw == 0).Select(BandRow) })).EnsureSuccessStatusCode();

        var (token, userId) = await VerifiedUser();
        var checkout = await Authed(HttpMethod.Post, $"/api/programs/{program.Id}/enroll", token, new { });
        checkout.EnsureSuccessStatusCode();
        var pay = (await checkout.Content.ReadFromJsonAsync<CheckoutSession>(Json))!;
        (await _client.PostAsync($"/api/dev/payments/{Uri.EscapeDataString(pay.ProviderRef)}/succeed", null))
            .EnsureSuccessStatusCode();

        return new Ctx(token, userId, admin, program.Id, session.Id, assessment.AssessmentId, key);
    }

    private async Task<AttemptStateDto> Start(Ctx c)
    {
        var res = await Authed(HttpMethod.Post, $"/api/assessments/{c.AssessmentId}/attempts", c.Token);
        res.EnsureSuccessStatusCode();
        var attempt = (await res.Content.ReadFromJsonAsync<AttemptDto>(Json))!;
        return await GetState(c, attempt.Id);
    }

    private Task<AttemptStateDto> GetState(Ctx c, Guid attemptId)
        => AuthedGet<AttemptStateDto>($"/api/attempts/{attemptId}/state", c.Token);

    private async Task<AttemptStateDto> SaveAnswers(Ctx c, Guid attemptId, Dictionary<string, int> answers)
    {
        var res = await Authed(HttpMethod.Post, $"/api/attempts/{attemptId}/section-answers", c.Token,
            new { answers });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AttemptStateDto>(Json))!;
    }

    private async Task<AttemptStateDto> Advance(Ctx c, Guid attemptId, Dictionary<string, int>? answers)
    {
        var res = await Authed(HttpMethod.Post, $"/api/attempts/{attemptId}/advance", c.Token, new { answers });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AttemptStateDto>(Json))!;
    }

    private async Task<AttemptResultDto> Finish(Ctx c, Guid attemptId)
    {
        var res = await Authed(HttpMethod.Post, $"/api/attempts/{attemptId}/finish", c.Token);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AttemptResultDto>(Json))!;
    }

    private async Task<ProctorStateDto> Proctor(Ctx c, Guid attemptId, string kind, int durationMs)
    {
        var res = await Authed(HttpMethod.Post, $"/api/attempts/{attemptId}/proctor-events", c.Token,
            new { kind, durationMs });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ProctorStateDto>(Json))!;
    }

    private Dictionary<string, int> CorrectFor(Ctx c, QuestionSection section)
        => c.Key[section].ToDictionary(q => q.Id.ToString(), q => q.Correct);

    /// <summary>Answers each section correctly and advances through all three.</summary>
    private async Task<AttemptResultDto> AnswerEverythingCorrectly(Ctx c, Guid attemptId)
    {
        foreach (var section in new[] { QuestionSection.Listening, QuestionSection.Structure, QuestionSection.Reading })
            await Advance(c, attemptId, CorrectFor(c, section));

        return await Finish(c, attemptId);
    }

    private async Task FinishAllSections(Ctx c, Guid attemptId)
    {
        for (var i = 0; i < 3; i++) await Advance(c, attemptId, null);
    }

    /// <summary>Rewinds every section clock so all deadlines are in the past.</summary>
    private async Task ExpireAllSectionsAsync(Guid attemptId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var attempt = await db.Attempts.FirstAsync(a => a.Id == attemptId);
        var state = JsonDocument.Parse(attempt.State);

        var sections = state.RootElement.GetProperty("sections").EnumerateArray()
            .Select(s => new
            {
                section = s.GetProperty("section").GetString(),
                minutes = s.GetProperty("minutes").GetInt32(),
            }).ToList();

        var longAgo = DateTimeOffset.UtcNow.AddDays(-1);
        var rebuilt = new
        {
            sections = sections.Select(s => new
            {
                section = s.section,
                minutes = s.minutes,
                startedAt = longAgo,
                submittedAt = (DateTimeOffset?)null,
            }).ToList(),
            currentIndex = 0,
            audioPlays = new Dictionary<string, int>(),
        };
        attempt.State = JsonSerializer.Serialize(rebuilt, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await db.SaveChangesAsync();
    }

    private async Task<(string token, Guid userId)> VerifiedUser()
    {
        var email = $"u{Guid.NewGuid():N}@test.local";
        (await _client.PostAsJsonAsync("/api/auth/register", new { name = "Budi", email, password = Pw }))
            .EnsureSuccessStatusCode();
        var token = TokenFrom(factory.Email.LastVerifyUrl);
        (await _client.PostAsJsonAsync("/api/auth/verify-email", new { token })).EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Pw });
        var tokens = (await login.Content.ReadFromJsonAsync<AuthTokens>())!;
        return (tokens.AccessToken, tokens.User.Id);
    }

    private async Task<string> AdminToken()
    {
        var email = $"adm{Guid.NewGuid():N}@test.local";
        (await _client.PostAsJsonAsync("/api/auth/register", new { name = "Adm", email, password = Pw }))
            .EnsureSuccessStatusCode();
        await WithDb(async db =>
        {
            var u = await db.Users.FirstAsync(x => x.Email == email);
            u.Role = UserRole.Admin;
            await db.SaveChangesAsync();
        });
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Pw });
        return (await login.Content.ReadFromJsonAsync<AuthTokens>())!.AccessToken;
    }

    private Task<HttpResponseMessage> Authed(HttpMethod method, string url, string token, object? body = null)
    {
        var req = new HttpRequestMessage(method, url)
        { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };
        if (body is not null) req.Content = JsonContent.Create(body);
        return _client.SendAsync(req);
    }

    private async Task<T> AuthedGet<T>(string url, string token)
    {
        var res = await Authed(HttpMethod.Get, url, token);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<T> PostJson<T>(string url, string token, object body)
    {
        var res = await Authed(HttpMethod.Post, url, token, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task WithDb(Func<AppDbContext, Task> action)
    {
        using var scope = factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<T> WithDbResult<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static string TokenFrom(string? url)
    {
        Assert.NotNull(url);
        var i = url!.IndexOf("token=", StringComparison.Ordinal);
        Assert.True(i >= 0, "verify URL has no token");
        return url[(i + "token=".Length)..];
    }
}
