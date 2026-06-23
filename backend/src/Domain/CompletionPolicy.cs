namespace Academy.Domain;

/// <summary>When a module counts as "completed" from watch progress (PRD §5.4 AC4).</summary>
public static class CompletionPolicy
{
    /// <summary>Default watch-through threshold (percent) that auto-completes a module.</summary>
    public const decimal ModuleCompleteThresholdPercent = 90m;

    public static bool IsModuleComplete(decimal percentComplete, decimal threshold = ModuleCompleteThresholdPercent)
        => percentComplete >= threshold;
}

/// <summary>Level completion rule that gates certificate issuance (GR-6). Quizzes/capstones
/// are non-gating in v1, so a level is complete once every published module is completed.</summary>
public static class LevelCompletion
{
    public static bool IsComplete(IReadOnlyCollection<Guid> publishedModuleIds, ISet<Guid> completedModuleIds)
        => publishedModuleIds.Count > 0 && publishedModuleIds.All(completedModuleIds.Contains);
}
