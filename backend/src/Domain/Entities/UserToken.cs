// Single-use email-verification / password-reset tokens (M1).
// Only the SHA-256 hash of the opaque token is stored; the raw token is emailed.
using Academy.Domain.Common;
using Academy.Domain.Enums;

namespace Academy.Domain.Entities;

public class UserToken : Entity
{
    public Guid UserId { get; set; }
    public UserTokenPurpose Purpose { get; set; }
    public string TokenHash { get; set; } = default!;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}
