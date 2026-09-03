using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Academy.Application.Auth;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Media;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>
/// Listening audio for a SESSION test (the gating test), which had no way to be played at all:
/// `GatingTest.tsx` contained no audio code and no endpoint existed to mint a URL for one. The
/// seeded sample test carries five Listening clips no learner could ever hear.
///
/// It went unnoticed because the sample's prompts print the conversation as text, so the questions
/// stayed answerable by reading. Real Listening content cannot do that — printing the script is
/// the answer — so this is what stops a session test holding genuine Listening items.
///
/// Unlike the final assessment there is NO play limit here. A gating test creates its attempt only
/// at submit, so while a learner is answering there is no attempt to charge plays against; and
/// gating tests allow unlimited retakes, so a cap has no teeth — fail, retry, hear it again.
/// </summary>
public class GatingAudioTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";
    private const string AudioKey = "audio/gating-listening-test.m4a";

    private record AudioUrl(string Url);

    private async Task<T> WithDb<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<HttpResponseMessage> Get(string url, string? token)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(req);
    }

    /// <summary>
    /// A programme with one video session carrying a gating test whose single question is a
    /// Listening item with its own clip, and a learner enrolled and unlocked on that session.
    /// </summary>
    private async Task<(string Token, Guid SessionId, Guid QuestionId)> SeedListeningTest(string suffix)
    {
        var email = $"gating-{suffix}-{Guid.NewGuid():N}@test.local";
        (await _client.PostAsJsonAsync("/api/auth/register", new { name = "Siswa", email, password = Pw }))
            .EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Pw });
        var tokens = (await login.Content.ReadFromJsonAsync<AuthTokens>())!;

        var (sessionId, questionId) = await WithDb(async db =>
        {
            var program = new Academy.Domain.Entities.Program
            {
                Id = Guid.CreateVersion7(),
                Name = $"Gating Audio {suffix}",
                Slug = $"gating-audio-{Guid.NewGuid():N}",
                Description = "d",
                PriceIdr = 1m,
                Status = ProgramStatus.Published,
            };
            db.Programs.Add(program);

            var question = new Question
            {
                Id = Guid.CreateVersion7(),
                Section = QuestionSection.Listening,
                Type = QuestionType.Mcq,
                Prompt = "Apa maksud pembicara?",
                Choices = """["a","b"]""",
                Correct = "[0]",
                AudioRef = AudioKey,
            };
            db.Questions.Add(question);

            var assessment = new Assessment
            {
                Id = Guid.CreateVersion7(),
                Kind = AssessmentKind.Gating,
                Title = "Tes sesi",
                Config = """{"passThreshold":1,"retakeCap":null,"proctoringEnabled":false,"sections":[]}""",
            };
            db.Assessments.Add(assessment);
            db.AssessmentQuestions.Add(new AssessmentQuestion
            {
                Id = Guid.CreateVersion7(), AssessmentId = assessment.Id, QuestionId = question.Id, OrderIndex = 0,
            });

            var session = new ProgramSession
            {
                Id = Guid.CreateVersion7(),
                ProgramId = program.Id,
                OrderIndex = 0,
                Type = SessionType.Video,
                Title = "Sesi 1",
                AssessmentId = assessment.Id,
            };
            db.ProgramSessions.Add(session);

            db.Enrollments.Add(new Enrollment
            {
                Id = Guid.CreateVersion7(),
                UserId = tokens.User.Id,
                ProgramId = program.Id,
                Status = EnrollmentStatus.Active,
                EnrolledAt = DateTimeOffset.UtcNow,
            });

            await db.SaveChangesAsync();
            return (session.Id, question.Id);
        });

        // The object must exist in storage, or a signed URL would resolve to nothing.
        using (var scope = factory.Services.CreateScope())
        {
            var storage = scope.ServiceProvider
                .GetRequiredService<Academy.Application.Abstractions.IObjectStorage>();
            await storage.PutAsync(AudioKey, new MemoryStream("listening-clip"u8.ToArray()), "audio/mp4");
        }

        return (tokens.AccessToken, sessionId, questionId);
    }

    [Fact]
    public async Task An_enrolled_learner_gets_a_signed_url_that_actually_plays()
    {
        var (token, sessionId, questionId) = await SeedListeningTest("play");

        var res = await Get($"/api/sessions/{sessionId}/assessment/audio/{questionId}", token);
        res.EnsureSuccessStatusCode();

        var url = (await res.Content.ReadFromJsonAsync<AudioUrl>(Json))!.Url;
        Assert.False(string.IsNullOrWhiteSpace(url));

        // The URL is the whole point — a signed link nobody can play is not a fix.
        var played = await _client.GetAsync(url);
        played.EnsureSuccessStatusCode();
        Assert.Equal("listening-clip", await played.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_url_is_signed_rather_than_a_bare_storage_path()
    {
        // GR-3: no public or persisted media URLs. Stripping the signature must break it.
        var (token, sessionId, questionId) = await SeedListeningTest("signed");

        var url = (await (await Get($"/api/sessions/{sessionId}/assessment/audio/{questionId}", token))
            .Content.ReadFromJsonAsync<AudioUrl>(Json))!.Url;

        Assert.Contains("sig=", url);
        var unsigned = url[..url.IndexOf('?')];
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(unsigned)).StatusCode);
    }

    [Fact]
    public async Task A_learner_who_is_not_enrolled_is_refused()
    {
        // THE GATE (GR-1). Audio is session content; reaching it must require session access.
        var (_, sessionId, questionId) = await SeedListeningTest("outsider");

        var email = $"outsider-{Guid.NewGuid():N}@test.local";
        (await _client.PostAsJsonAsync("/api/auth/register", new { name = "Lain", email, password = Pw }))
            .EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Pw });
        var outsider = (await login.Content.ReadFromJsonAsync<AuthTokens>())!.AccessToken;

        var res = await Get($"/api/sessions/{sessionId}/assessment/audio/{questionId}", outsider);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task An_anonymous_request_is_refused()
    {
        var (_, sessionId, questionId) = await SeedListeningTest("anon");

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Get($"/api/sessions/{sessionId}/assessment/audio/{questionId}", null)).StatusCode);
    }

    [Fact]
    public async Task A_question_from_a_different_assessment_is_refused()
    {
        // Without this the route is a way to read ANY audio key by guessing question ids — the
        // learner has access to THIS session, not to every clip in the bank.
        var (token, sessionId, _) = await SeedListeningTest("scope");
        var (_, _, foreignQuestionId) = await SeedListeningTest("elsewhere");

        var res = await Get($"/api/sessions/{sessionId}/assessment/audio/{foreignQuestionId}", token);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task A_question_with_no_audio_is_a_clear_refusal_not_a_broken_url()
    {
        var (token, sessionId, _) = await SeedListeningTest("silent");

        var silentId = await WithDb(async db =>
        {
            var assessmentId = await db.ProgramSessions
                .Where(s => s.Id == sessionId).Select(s => s.AssessmentId!.Value).FirstAsync();
            var q = new Question
            {
                Id = Guid.CreateVersion7(),
                Section = QuestionSection.Reading,
                Type = QuestionType.Mcq,
                Prompt = "Tanpa audio",
                Choices = """["a","b"]""",
                Correct = "[0]",
                AudioRef = null,
            };
            db.Questions.Add(q);
            db.AssessmentQuestions.Add(new AssessmentQuestion
            {
                Id = Guid.CreateVersion7(), AssessmentId = assessmentId, QuestionId = q.Id, OrderIndex = 1,
            });
            await db.SaveChangesAsync();
            return q.Id;
        });

        var res = await Get($"/api/sessions/{sessionId}/assessment/audio/{silentId}", token);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("tidak memiliki audio", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Playing_twice_is_allowed_because_a_gating_test_has_unlimited_retakes()
    {
        // Deliberate difference from the final assessment. The attempt is created at SUBMIT, so
        // while answering there is nothing to charge a play against — and with unlimited retakes a
        // cap would only cost a learner a click.
        var (token, sessionId, questionId) = await SeedListeningTest("twice");

        for (var i = 0; i < 3; i++)
        {
            var res = await Get($"/api/sessions/{sessionId}/assessment/audio/{questionId}", token);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }
    }
}
