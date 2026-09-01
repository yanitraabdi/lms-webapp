using Academy.Application.Assessments;

namespace Academy.Application.Tests;

/// <summary>
/// The importer and the bulk upload must compute the SAME key from the same filename, or a
/// question points at an object that was stored elsewhere. The key also lands in a URL that the
/// media route validates, so this doubles as a trust boundary: it must never emit a key that
/// LocalObjectStorage.IsValidKey would reject, and never one that escapes the storage root.
/// </summary>
public class AudioKeyTests
{
    [Theory]
    [InlineData("L01.mp3", "audio/l01.mp3")]
    [InlineData("l01.MP3", "audio/l01.mp3")]
    [InlineData("Section 1 - Part A.m4a", "audio/section1-parta.m4a")]
    [InlineData("R07_final.wav", "audio/r07_final.wav")]
    public void A_filename_maps_to_a_lowercase_key(string filename, string expected)
        => Assert.Equal(expected, AudioKey.FromFilename(filename));

    [Fact]
    public void The_importer_and_the_uploader_agree_on_one_key()
    {
        // The uploader knows the content type and takes the extension from it; the importer has
        // only the sheet's filename. For a correctly-named file they must land on the same key.
        Assert.Equal(
            AudioKey.Build("L01", ".mp3"),
            AudioKey.FromFilename("L01.mp3"));
    }

    [Theory]
    [InlineData("../../etc/passwd.mp3")]     // directory traversal
    [InlineData("audio/../x.mp3")]
    [InlineData("..mp3")]
    [InlineData("...")]
    [InlineData("---.mp3")]                  // sanitises to nothing usable
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("noextension")]
    [InlineData("bad.")]
    public void An_unusable_or_escaping_filename_yields_null(string filename)
        => Assert.Null(AudioKey.FromFilename(filename));

    [Fact]
    public void A_traversal_attempt_never_keeps_its_path_segments()
    {
        // Even if a future change made the leading dots survivable, the key must stay one segment.
        var key = AudioKey.FromFilename("evil/../../L01.mp3");
        Assert.True(key is null || key == "audio/l01.mp3");
    }

    [Fact]
    public void A_very_long_basename_is_truncated_to_a_valid_key()
    {
        var key = AudioKey.FromFilename(new string('a', 500) + ".mp3");
        Assert.NotNull(key);
        Assert.True(key!.Length <= 200);          // LocalObjectStorage.IsValidKey caps at 200
    }

    [Fact]
    public void Null_is_tolerated()
        => Assert.Null(AudioKey.FromFilename(null));
}
