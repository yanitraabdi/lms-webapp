namespace Academy.Domain;

/// <summary>
/// Core access rule (GR-1): a module is accessible iff it is a free preview, or the user has an
/// active subscription whose tier is at least the module's required tier. Pure + side-effect free
/// so it can be unit-tested and reused wherever access is evaluated server-side.
/// </summary>
public static class Entitlement
{
    public static bool CanAccess(int? activeTier, bool isPreview, int requiredPlanTier)
        => isPreview || (activeTier is int tier && tier >= requiredPlanTier);
}
