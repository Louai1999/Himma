namespace Himma.Api.Features.Auth;

// Request models contain only what the client may set. There is deliberately no role field:
// every registration becomes a normal User, and unknown JSON fields are rejected.

public sealed class RegisterRequest
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool AcceptPrivacy { get; init; }
}

public sealed class LoginRequest
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}

public sealed record AuthUser(string Id, string Username, string DisplayName, IReadOnlyList<string> Roles);

/// <summary>The access token goes in the body (kept in memory by the client); the refresh token goes in the cookie.</summary>
public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, AuthUser User);
