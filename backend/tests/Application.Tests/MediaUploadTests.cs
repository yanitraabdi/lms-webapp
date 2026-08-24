using Academy.Application.Assessments;

namespace Academy.Application.Tests;

/// <summary>
/// Upload validation is pure so it can be tested without pushing 100 MB through HTTP.
/// </summary>
public class MediaUploadTests
{
    private const long Max = 1024;

    [Theory]
    [InlineData("audio/mpeg", ".mp3")]
    [InlineData("audio/mp4", ".m4a")]
    [InlineData("audio/x-m4a", ".m4a")]
    [InlineData("audio/wav", ".wav")]
    [InlineData("audio/ogg", ".ogg")]
    public void Accepted_types_pass_and_map_to_an_extension(string type, string ext)
    {
        Assert.Null(MediaUpload.Validate(type, 100, Max));
        Assert.Equal(ext, MediaUpload.ExtensionFor(type));
    }

    [Theory]
    [InlineData("video/mp4")]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    [InlineData("")]
    [InlineData(null)]
    public void Other_types_are_refused(string? type)
        => Assert.NotNull(MediaUpload.Validate(type, 100, Max));

    [Fact]
    public void An_oversize_file_is_refused_and_the_message_names_the_limit()
    {
        var error = MediaUpload.Validate("audio/mpeg", Max + 1, Max);
        Assert.NotNull(error);
        Assert.Contains("1", error);
    }

    [Fact]
    public void An_empty_file_is_refused()
        => Assert.NotNull(MediaUpload.Validate("audio/mpeg", 0, Max));

    [Fact]
    public void A_type_with_charset_parameters_still_matches()
        => Assert.Null(MediaUpload.Validate("audio/mpeg; charset=binary", 100, Max));
}
