using Microsoft.Extensions.Options;

namespace Himma.Api.Infrastructure.Auth;

/// <summary>
/// The refresh token travels only in this cookie: HttpOnly (JavaScript cannot read it), Secure,
/// SameSite=Strict (not sent by other sites), and limited to /api/auth.
/// The __Secure- prefix makes browsers refuse it unless it is Secure.
/// </summary>
public sealed class RefreshCookie(IOptions<JwtOptions> options)
{
    public const string Name = "__Secure-himma_refresh";
    public const string CookiePath = "/api/auth";

    public string? Read(HttpRequest request) => request.Cookies[Name];

    public void Write(HttpResponse response, string rawToken) =>
        response.Cookies.Append(Name, rawToken, Build(DateTimeOffset.UtcNow.AddDays(options.Value.RefreshTokenDays)));

    public void Delete(HttpResponse response) => response.Cookies.Delete(Name, Build(expires: null));

    private static CookieOptions Build(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        Expires = expires,
        IsEssential = true,
    };
}
