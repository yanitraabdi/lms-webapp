using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Academy.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Academy.Infrastructure.Auth;

public class JwtTokenService(IOptions<AuthOptions> options)
{
    private readonly AuthOptions _o = options.Value;

    public (string token, int expiresInSeconds) CreateAccessToken(User user)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_o.AccessTokenMinutes);

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.JwtSigningKey)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.Name),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("email_verified", user.EmailVerified ? "true" : "false"),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
        };

        var jwt = new JwtSecurityToken(_o.JwtIssuer, _o.JwtAudience, claims, now, expires, creds);
        return (new JwtSecurityTokenHandler().WriteToken(jwt), _o.AccessTokenMinutes * 60);
    }
}
