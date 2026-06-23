using Academy.Domain;

namespace Academy.Domain.Tests;

public class CompletionTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(89.99, false)]
    [InlineData(90, true)]
    [InlineData(100, true)]
    public void IsModuleComplete_uses_90pct_default(decimal percent, bool expected)
        => Assert.Equal(expected, CompletionPolicy.IsModuleComplete(percent));

    [Fact]
    public void IsModuleComplete_respects_custom_threshold()
    {
        Assert.True(CompletionPolicy.IsModuleComplete(80m, threshold: 75m));
        Assert.False(CompletionPolicy.IsModuleComplete(74m, threshold: 75m));
    }

    [Fact]
    public void LevelComplete_true_when_all_published_completed()
    {
        var published = new[] { G(1), G(2), G(3) };
        var completed = new HashSet<Guid> { G(1), G(2), G(3) };
        Assert.True(LevelCompletion.IsComplete(published, completed));
    }

    [Fact]
    public void LevelComplete_false_when_any_published_incomplete()
    {
        var published = new[] { G(1), G(2), G(3) };
        var completed = new HashSet<Guid> { G(1), G(2) };
        Assert.False(LevelCompletion.IsComplete(published, completed));
    }

    [Fact]
    public void LevelComplete_false_when_no_published_modules()
        => Assert.False(LevelCompletion.IsComplete(Array.Empty<Guid>(), new HashSet<Guid> { G(1) }));

    [Fact]
    public void LevelComplete_ignores_completed_beyond_published_set()
    {
        // Immutability friend: extra completed modules (e.g. bonus) don't break completeness.
        var published = new[] { G(1), G(2) };
        var completed = new HashSet<Guid> { G(1), G(2), G(9) };
        Assert.True(LevelCompletion.IsComplete(published, completed));
    }

    private static Guid G(int n) => new($"00000000-0000-0000-0000-{n:D12}");
}
