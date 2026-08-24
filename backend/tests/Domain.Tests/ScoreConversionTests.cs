using Academy.Domain;

namespace Academy.Domain.Tests;

/// <summary>
/// The ITP conversion arithmetic (KAK §9.9.3). The conversion VALUES are admin data; these tests
/// pin the formula and its published bounds so a data change can never silently break the maths.
/// </summary>
public class ScoreConversionTests
{
    [Theory]
    // Published anchor points of the ITP total formula.
    [InlineData(31, 31, 31, 310)]     // floor
    [InlineData(68, 68, 67, 677)]     // ceiling
    [InlineData(50, 50, 50, 500)]     // exact division
    [InlineData(60, 55, 58, 577)]     // (173)*10/3 = 576.67 → 577
    [InlineData(45, 45, 46, 453)]     // (136)*10/3 = 453.33 → 453
    public void Total_matches_published_anchors(int l, int s, int r, int expected)
        => Assert.Equal(expected, ToeflScoring.Total(l, s, r));

    [Fact]
    public void Total_is_symmetric_across_sections()
    {
        // The formula sums the three sections, so ordering must not matter.
        Assert.Equal(ToeflScoring.Total(60, 50, 40), ToeflScoring.Total(40, 50, 60));
        Assert.Equal(ToeflScoring.Total(60, 50, 40), ToeflScoring.Total(50, 60, 40));
    }

    [Fact]
    public void Total_is_monotonic_in_each_section()
    {
        var baseline = ToeflScoring.Total(50, 50, 50);
        Assert.True(ToeflScoring.Total(51, 50, 50) >= baseline);
        Assert.True(ToeflScoring.Total(50, 51, 50) >= baseline);
        Assert.True(ToeflScoring.Total(50, 50, 51) >= baseline);
        Assert.True(ToeflScoring.Total(49, 50, 50) <= baseline);
    }

    [Fact]
    public void Reading_ceiling_is_one_lower_than_the_other_sections()
    {
        // ITP convention: Reading tops out at 67 while Listening/Structure reach 68.
        Assert.True(ToeflScoring.IsValidScaled(68, ToeflScoring.ListeningScaledMax));
        Assert.True(ToeflScoring.IsValidScaled(68, ToeflScoring.StructureScaledMax));
        Assert.False(ToeflScoring.IsValidScaled(68, ToeflScoring.ReadingScaledMax));
    }
}
