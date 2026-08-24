using Academy.Domain.Enums;

namespace Academy.Application.Assessments;

/// <summary>
/// Which audio object a question plays, and which counter its plays are charged to.
/// Pure — no EF, no HTTP — so both the URL-minting path and the state projection can share it.
/// </summary>
public static class AudioResolution
{
    /// <summary>The storage key to serve, or null when the question has no audio at all.</summary>
    public static string? StorageKey(string? questionAudioRef, QuestionSection section, AssessmentConfig config)
    {
        if (!string.IsNullOrWhiteSpace(questionAudioRef)) return questionAudioRef;

        var sectionRef = config.Sections.FirstOrDefault(s => s.Section == section)?.AudioRef;
        return string.IsNullOrWhiteSpace(sectionRef) ? null : sectionRef;
    }

    /// <summary>
    /// The AttemptState.AudioPlays key, or null when there is no audio. A per-question clip is
    /// charged to the question; a section recording is charged to the section, so replaying it
    /// from three different questions consumes ONE shared allowance — there is only one audio.
    /// </summary>
    public static string? PlayKey(Guid questionId, string? questionAudioRef, QuestionSection section, AssessmentConfig config)
    {
        if (!string.IsNullOrWhiteSpace(questionAudioRef)) return questionId.ToString();

        var sectionRef = config.Sections.FirstOrDefault(s => s.Section == section)?.AudioRef;
        return string.IsNullOrWhiteSpace(sectionRef) ? null : section.ToString();
    }
}
