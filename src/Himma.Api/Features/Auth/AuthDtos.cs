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

public sealed class ForgotPasswordRequest
{
    public string Email { get; init; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    public string Email { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}

/// <summary>
/// MustChangePassword: an admin gave a temporary password, so the app asks for a new one first.
/// HasEmail: a confirmed recovery email exists (the app suggests adding one when it does not).
/// </summary>
public sealed record AuthUser(string Id, string Username, string DisplayName, IReadOnlyList<string> Roles, bool MustChangePassword, bool HasEmail);

/// <summary>What the sign-in screen can offer before anyone is signed in.</summary>
public sealed record AuthOptionsResponse(bool EmailEnabled);

/// <summary>The access token goes in the body (kept in memory by the client); the refresh token goes in the cookie.</summary>
public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, AuthUser User);
