using System.Security.Claims;
using System.Text;
using Himma.Api.Data;
using Himma.Api.Features.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Himma.Api.Infrastructure.Auth;

public static class AuthSetup
{
    public static IServiceCollection AddHimmaAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => Encoding.UTF8.GetByteCount(o.Key) >= 32,
                "Jwt:Key is missing or shorter than 32 characters. Set it with: dotnet user-secrets set \"Jwt:Key\" \"<long random value>\"")
            .Validate(o => o.AccessTokenMinutes is >= 1 and <= 60, "Jwt:AccessTokenMinutes must be between 1 and 60.")
            .Validate(o => o.RefreshTokenDays is >= 7 and <= 30, "Jwt:RefreshTokenDays must be between 7 and 30.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configured lazily so the key is read from the final configuration (user-secrets, env vars, tests).
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = TokenService.CreateSigningKey(jwt.Key),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = HimmaClaims.Username,
                    RoleClaimType = HimmaClaims.Role,
                };
                bearer.Events = new JwtBearerEvents { OnTokenValidated = RejectInactiveAccountsAsync };
            });

        services.AddAuthorization();

        services.AddScoped<TokenService>();
        services.AddScoped<RefreshTokenService>();
        services.AddScoped<AuthService>();
        services.AddSingleton<RefreshCookie>();

        return services;
    }

    /// <summary>
    /// Runs on every authenticated request: a valid signature is not enough, the account must still be
    /// active, not deleted, and its security stamp must match (so a password change or suspension
    /// invalidates access tokens immediately instead of after 15 minutes).
    /// </summary>
    private static async Task RejectInactiveAccountsAsync(TokenValidatedContext context)
    {
        var userId = context.Principal?.FindFirstValue(HimmaClaims.Subject);
        var stamp = context.Principal?.FindFirstValue(HimmaClaims.SecurityStamp);
        if (string.IsNullOrEmpty(userId) || stamp is null)
        {
            context.Fail("Token is missing required claims.");
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var account = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, u.DeletedAtUtc, u.SecurityStamp })
            .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

        if (account is null || !account.IsActive || account.DeletedAtUtc is not null || account.SecurityStamp != stamp)
        {
            context.Fail("Account is not allowed to use this token.");
        }
    }
}
