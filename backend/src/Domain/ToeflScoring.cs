namespace Academy.Domain;

/// <summary>
/// TOEFL ITP scoring rules (KAK §9.7.1, §9.9). Pure — no EF, no HTTP.
/// The raw→scaled conversion VALUES are admin-maintained data (score_band_mappings); this type
/// owns only the arithmetic and the validity bounds, which are format constants.
/// </summary>
public static class ToeflScoring
{
    // Section sizes (KAK §9.7.1) — question counts of the ITP Level 1 format.
    public const int ListeningQuestions = 50;
    public const int StructureQuestions = 40;
    public const int ReadingQuestions = 50;
    public const int TotalQuestions = ListeningQuestions + StructureQuestions + ReadingQuestions; // 140

    // Per-section time limits, in minutes.
    public const int ListeningMinutes = 35;
    public const int StructureMinutes = 25;
    public const int ReadingMinutes = 55;
    public const int TotalMinutes = ListeningMinutes + StructureMinutes + ReadingMinutes;         // 115

    // Scaled-score bounds. Reading tops out one point lower than the other two (ITP convention).
    public const int ScaledMin = 31;
    public const int ListeningScaledMax = 68;
    public const int StructureScaledMax = 68;
    public const int ReadingScaledMax = 67;

    // Resulting total bounds: 31*3*10/3 = 310 … (68+68+67)*10/3 = 676.67 → 677.
    public const int TotalMin = 310;
    public const int TotalMax = 677;

    /// <summary>
    /// Total = ROUND((Listening + Structure + Reading) * 10 / 3), per the ITP convention.
    /// Uses away-from-zero rounding so a .5 rounds up, matching published conversion tables.
    /// </summary>
    public static int Total(int listeningScaled, int structureScaled, int readingScaled)
        => (int)Math.Round((listeningScaled + structureScaled + readingScaled) * 10m / 3m,
                           MidpointRounding.AwayFromZero);

    /// <summary>Whether a scaled section score is inside its allowed band.</summary>
    public static bool IsValidScaled(int scaled, int sectionMax)
        => scaled >= ScaledMin && scaled <= sectionMax;

    /// <summary>Whether a computed total is inside the ITP range.</summary>
    public static bool IsValidTotal(int total) => total is >= TotalMin and <= TotalMax;
}
