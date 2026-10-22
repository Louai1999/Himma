namespace Himma.Api.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "himma";
    public string Audience { get; set; } = "himma-web";

    /// <summary>HMAC signing key, at least 32 bytes. Secret: user-secrets locally, environment variable in production.</summary>
    public string Key { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;
}

/// <summary>Claim names used in Himma access tokens.</summary>
public static class HimmaClaims
{
    public const string Subject = "sub";
    public const string Username = "name";
    public const string Role = "role";

    /// <summary>Identity security stamp; changes on password change or suspension, which invalidates old tokens.</summary>
    public const string SecurityStamp = "stamp";
}
