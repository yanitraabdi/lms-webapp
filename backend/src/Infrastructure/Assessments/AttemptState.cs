using System.Text.Json;
using Academy.Application.Assessments;

namespace Academy.Infrastructure.Assessments;

/// <summary>
/// Parsed form of <c>attempts.state</c> (jsonb). Holds ONLY server-stamped facts: when each
/// section opened and closed, which section is active, and how many times each audio clip was
/// played. Deadlines are derived from these, never supplied by the client (GR-12).
/// </summary>
internal class AttemptState
{
    public List<SectionState> Sections { get; set; } = [];
    public int CurrentIndex { get; set; }
    public Dictionary<string, int> AudioPlays { get; set; } = [];

    public SectionState? Current =>
        CurrentIndex >= 0 && CurrentIndex < Sections.Count ? Sections[CurrentIndex] : null;

    public bool IsLastSection => CurrentIndex >= Sections.Count - 1;

    /// <summary>Builds the initial state from the assessment config and opens the first section.</summary>
    public static AttemptState Create(AssessmentConfig config, DateTimeOffset now)
    {
        var state = new AttemptState();
        foreach (var s in config.Sections)
            state.Sections.Add(new SectionState
            {
                Section = s.Section.ToString(),
                Minutes = s.Minutes,
            });

        if (state.Sections.Count > 0) state.Sections[0].StartedAt = now;
        return state;
    }

    /// <summary>Deadline of the active section, or null when it is untimed / already closed.</summary>
    public DateTimeOffset? DeadlineOf(SectionState section)
        => section.StartedAt is DateTimeOffset started && section.Minutes > 0
            ? started.AddMinutes(section.Minutes)
            : null;

    public static AttemptState Parse(string json)
    {
        try { return JsonSerializer.Deserialize<AttemptState>(json, Opts) ?? new AttemptState(); }
        catch { return new AttemptState(); }
    }

    public string Serialize() => JsonSerializer.Serialize(this, Opts);

    private static readonly JsonSerializerOptions Opts = new(JsonSerializerDefaults.Web);
}

internal class SectionState
{
    public string Section { get; set; } = "";
    public int Minutes { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }

    /// <summary>True once the learner has left it — sections are non-returnable (KAK §9.7.2 R3).</summary>
    public bool Closed => SubmittedAt is not null;
}
