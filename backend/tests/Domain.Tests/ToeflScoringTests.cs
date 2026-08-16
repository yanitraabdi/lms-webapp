using Academy.Domain;

namespace Academy.Domain.Tests;

public class ToeflScoringTests
{
    [Fact]
    public void Format_constants_match_ITP_level_1()
    {
        Assert.Equal(140, ToeflScoring.TotalQuestions);
        Assert.Equal(115, ToeflScoring.TotalMinutes);
    }

    [Theory]
    // Floor: every section at the minimum scaled score → 310.
    [InlineData(31, 31, 31, 310)]
    // Ceiling: (68+68+67)*10/3 = 676.67 → 677.
    [InlineData(68, 68, 67, 677)]
    // A representative mid-range sitting: (52+50+51)*10/3 = 510.
    [InlineData(52, 50, 51, 510)]
    // Rounding away from zero: (50+50+51)*10/3 = 503.33 → 503.
    [InlineData(50, 50, 51, 503)]
    // (51+50+51)*10/3 = 506.67 → 507.
    [InlineData(51, 50, 51, 507)]
    public void Total_follows_the_ITP_formula(int listening, int structure, int reading, int expected)
        => Assert.Equal(expected, ToeflScoring.Total(listening, structure, reading));

    [Fact]
    public void Total_stays_within_the_published_range_across_the_whole_domain()
    {
        for (var l = ToeflScoring.ScaledMin; l <= ToeflScoring.ListeningScaledMax; l++)
            for (var s = ToeflScoring.ScaledMin; s <= ToeflScoring.StructureScaledMax; s++)
                for (var r = ToeflScoring.ScaledMin; r <= ToeflScoring.ReadingScaledMax; r++)
                    Assert.True(ToeflScoring.IsValidTotal(ToeflScoring.Total(l, s, r)));
    }

    [Theory]
    [InlineData(31, ToeflScoring.ListeningScaledMax, true)]   // lower bound inclusive
    [InlineData(68, ToeflScoring.ListeningScaledMax, true)]   // upper bound inclusive
    [InlineData(30, ToeflScoring.ListeningScaledMax, false)]  // below floor
    [InlineData(69, ToeflScoring.ListeningScaledMax, false)]  // above ceiling
    [InlineData(68, ToeflScoring.ReadingScaledMax, false)]    // Reading tops out at 67, not 68
    [InlineData(67, ToeflScoring.ReadingScaledMax, true)]
    public void Scaled_bounds_are_enforced_per_section(int scaled, int sectionMax, bool expected)
        => Assert.Equal(expected, ToeflScoring.IsValidScaled(scaled, sectionMax));

    [Theory]
    [InlineData(309, false)]
    [InlineData(310, true)]
    [InlineData(677, true)]
    [InlineData(678, false)]
    public void Total_validity_bounds(int total, bool expected)
        => Assert.Equal(expected, ToeflScoring.IsValidTotal(total));
}
