using Academy.Application.Assessments;
using Academy.Domain.Enums;

namespace Academy.Application.Tests;

/// <summary>
/// Audio may arrive as one clip per question or as one recording per section. A question's own
/// clip wins, so a single item can be re-recorded without replacing the whole section file.
/// </summary>
public class AudioResolutionTests
{
    private static AssessmentConfig Config(string? sectionAudio) => new()
    {
        Sections =
        [
            new() { Section = QuestionSection.Listening, Questions = 50, Minutes = 35, AudioRef = sectionAudio },
            new() { Section = QuestionSection.Structure, Questions = 40, Minutes = 25 },
        ],
    };

    private static readonly Guid Q = Guid.CreateVersion7();

    [Fact]
    public void A_questions_own_clip_wins_over_the_section_recording()
    {
        var c = Config("audio/section.mp3");
        Assert.Equal("audio/mine.mp3",
            AudioResolution.StorageKey("audio/mine.mp3", QuestionSection.Listening, c));
        Assert.Equal(Q.ToString(),
            AudioResolution.PlayKey(Q, "audio/mine.mp3", QuestionSection.Listening, c));
    }

    [Fact]
    public void Without_its_own_clip_a_question_falls_back_to_the_section_recording()
    {
        var c = Config("audio/section.mp3");
        Assert.Equal("audio/section.mp3",
            AudioResolution.StorageKey(null, QuestionSection.Listening, c));
    }

    [Fact]
    public void Section_audio_is_counted_under_one_shared_key()
    {
        var c = Config("audio/section.mp3");
        var other = Guid.CreateVersion7();

        // Two different questions, one allowance — there is only one recording.
        Assert.Equal("Listening", AudioResolution.PlayKey(Q, null, QuestionSection.Listening, c));
        Assert.Equal("Listening", AudioResolution.PlayKey(other, null, QuestionSection.Listening, c));
    }

    [Fact]
    public void With_neither_there_is_no_audio()
    {
        var c = Config(null);
        Assert.Null(AudioResolution.StorageKey(null, QuestionSection.Listening, c));
        Assert.Null(AudioResolution.PlayKey(Q, null, QuestionSection.Listening, c));
    }

    [Fact]
    public void A_section_absent_from_the_config_has_no_audio()
    {
        var c = Config("audio/section.mp3");
        Assert.Null(AudioResolution.StorageKey(null, QuestionSection.Reading, c));
    }

    [Fact]
    public void Blank_refs_count_as_absent()
    {
        var c = Config("   ");
        Assert.Null(AudioResolution.StorageKey("  ", QuestionSection.Listening, c));
        Assert.Null(AudioResolution.PlayKey(Q, "", QuestionSection.Listening, c));
    }
}
