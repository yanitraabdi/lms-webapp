using Academy.Domain.Enums;

namespace Academy.Domain;

public enum PartStatus { Locked, Open, Done }

/// <summary>What one learner has done on one part. <see cref="Done"/>: video watched ≥ threshold,
/// test passed. The failure fields are read only on a Test.</summary>
public readonly record struct PartFacts(
    SessionPartKind Kind, bool Done, int FailedAttempts = 0, int? DiscussionAfterFailures = null);

/// <summary>
/// The linear lock INSIDE a video session (spec 2026-10-05 §3). Pure — no EF, no HTTP.
/// A part opens when every part before it is done. The one exception is a discussion video, which
/// opens as soon as the test directly before it is passed OR has failed N times. Because a
/// discussion counts toward "every part before", the part after a test+discussion pair needs both
/// the pass and the discussion watched.
/// </summary>
public static class SessionParts
{
    public static IReadOnlyList<PartStatus> Statuses(IReadOnlyList<PartFacts> parts)
    {
        var result = new PartStatus[parts.Count];
        var allBeforeDone = true;

        for (var i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            var open = p.Kind == SessionPartKind.Discussion
                ? DiscussionOpen(parts, result, i)
                : allBeforeDone;

            result[i] = !open ? PartStatus.Locked : p.Done ? PartStatus.Done : PartStatus.Open;
            allBeforeDone &= p.Done;
        }
        return result;
    }

    /// <summary>Every part done. No parts ⇒ never complete (an empty session must not unlock the next).</summary>
    public static bool IsComplete(IReadOnlyList<PartFacts> parts)
        => parts.Count > 0 && parts.All(p => p.Done);

    private static bool DiscussionOpen(IReadOnlyList<PartFacts> parts, PartStatus[] statuses, int i)
    {
        if (i == 0 || parts[i - 1].Kind != SessionPartKind.Test || statuses[i - 1] == PartStatus.Locked)
            return false;
        var test = parts[i - 1];
        return test.Done || (test.DiscussionAfterFailures is int n && test.FailedAttempts >= n);
    }
}
