using System.Text;
using Academy.Infrastructure.Media;

namespace Academy.Integration.Tests;

/// <summary>
/// Local object storage is the dev-sim behind IObjectStorage. The key comes from a URL on the
/// serve route, so key validation is a trust boundary, not a tidiness rule.
/// </summary>
public class LocalObjectStorageTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "inverta-storage-" + Guid.CreateVersion7().ToString("N"));

    private LocalObjectStorage Storage() =>
        new(new MediaOptions { Root = _root });

    [Fact]
    public async Task Put_then_open_round_trips_the_bytes()
    {
        var s = Storage();
        await s.PutAsync("audio/clip.mp3", new MemoryStream("hello"u8.ToArray()), "audio/mpeg");

        await using var read = await s.OpenReadAsync("audio/clip.mp3");
        Assert.NotNull(read);
        using var r = new StreamReader(read!, Encoding.UTF8);
        Assert.Equal("hello", await r.ReadToEndAsync());
    }

    [Fact]
    public async Task Put_overwrites_an_existing_object()
    {
        var s = Storage();
        await s.PutAsync("audio/clip.mp3", new MemoryStream("first"u8.ToArray()), "audio/mpeg");
        await s.PutAsync("audio/clip.mp3", new MemoryStream("second"u8.ToArray()), "audio/mpeg");

        await using var read = await s.OpenReadAsync("audio/clip.mp3");
        using var r = new StreamReader(read!, Encoding.UTF8);
        Assert.Equal("second", await r.ReadToEndAsync());
    }

    [Fact]
    public async Task Open_returns_null_for_an_unknown_key()
        => Assert.Null(await Storage().OpenReadAsync("audio/nope.mp3"));

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("audio/../../etc/passwd")]
    [InlineData("/etc/passwd")]
    [InlineData("audio\\..\\..\\secret")]
    [InlineData("audio/")]
    [InlineData("nofolder.mp3")]
    [InlineData("")]
    public async Task Traversal_and_malformed_keys_are_refused(string key)
    {
        var s = Storage();
        Assert.False(LocalObjectStorage.IsValidKey(key));
        await Assert.ThrowsAsync<ArgumentException>(
            () => s.PutAsync(key, new MemoryStream([1, 2, 3]), "audio/mpeg"));
        Assert.Null(await s.OpenReadAsync(key));
    }

    [Fact]
    public async Task Nothing_is_written_outside_the_root()
    {
        var s = Storage();
        await s.PutAsync("audio/clip.mp3", new MemoryStream([1, 2, 3]), "audio/mpeg");

        Assert.True(File.Exists(Path.Combine(_root, "audio", "clip.mp3")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }
}
