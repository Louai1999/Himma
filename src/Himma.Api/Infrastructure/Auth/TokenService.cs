using System.Security.Claims;
using System.Text;
using Himma.Api.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Himma.Api.Infrastructure.Auth;

/// <summary>Creates short-lived JWT access tokens.</summary>
public sealed class TokenService(IOptions<JwtOptions> options)
{
    private static readonly JsonWebTokenHandler Handler = new();

    public static SymmetricSecurityKey CreateSigningKey(string key) => new(Encoding.UTF8.GetBytes(key));

    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(ApplicationUser user, IEnumerable<string> roles)
    {
        var jwt = options.Value;
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(jwt.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(HimmaClaims.Subject, user.Id),
            new(HimmaClaims.Username, user.UserName ?? string.Empty),
            new(HimmaClaims.SecurityStamp, user.SecurityStamp ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        claims.AddRange(roles.Select(role => new Claim(HimmaClaims.Role, role)));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(CreateSigningKey(jwt.Key), SecurityAlgorithms.HmacSha256),
        };

        return (Handler.CreateToken(descriptor), expires);
    }
}
