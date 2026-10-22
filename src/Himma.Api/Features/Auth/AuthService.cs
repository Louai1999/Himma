using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Auth;
using Himma.Api.Infrastructure.Identity;
using Himma.Api.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Himma.Api.Infrastructure.Localization;

namespace Himma.Api.Features.Auth;

public enum AuthStatus
{
    Success,
    InvalidCredentials,
    AccountDisabled,
    AccountPendingDeletion,
    UsernameTaken,
    ValidationFailed,
    InvalidRefreshToken,
}

public sealed record AuthOutcome(
    AuthStatus Status,
    AuthResponse? Response = null,
    string? RefreshToken = null,
    IDictionary<string, string[]>? Errors = null);

public sealed class AuthService(
    UserManager<ApplicationUser> users,
    TokenService tokens,
    RefreshTokenService refreshTokens,
    IOptions<AdminOptions> adminOptions,
    ILogger<AuthService> logger)
{
    // Checked against when the username does not exist, so "unknown user" takes as long as "wrong password".
    private static readonly Lazy<string> DummyPasswordHash = new(() =>
        new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), Guid.NewGuid().ToString()));

    public async Task<AuthOutcome> RegisterAsync(RegisterRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        // The admin username is reserved even before the admin is seeded, and looks like any taken name.
        if (IsAdminUsername(request.Username) || await users.FindByNameAsync(request.Username) is not null)
        {
            return new AuthOutcome(AuthStatus.UsernameTaken);
        }

        var now = DateTime.UtcNow;
        var user = new ApplicationUser
        {
            UserName = request.Username,
            DisplayName = request.DisplayName.Trim(),
            CreatedAtUtc = now,
            LastLoginAtUtc = now,
            PrivacyConsentAtUtc = now,
            IsActive = true,
            LockoutEnabled = true,
        };

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            if (created.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateUserName)))
            {
                return new AuthOutcome(AuthStatus.UsernameTaken);
            }
            return new AuthOutcome(AuthStatus.ValidationFailed, Errors: ToErrors(created, passwordField: "password"));
        }

        await users.AddToRoleAsync(user, Roles.User);
        logger.LogInformation("New user registered {UserId}", user.Id);

        return await StartSessionAsync(user, client, cancellationToken);
    }

    public async Task<AuthOutcome> LoginAsync(LoginRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        var user = await users.FindByNameAsync(request.Username);
        if (user is null)
        {
            users.PasswordHasher.VerifyHashedPassword(new ApplicationUser(), DummyPasswordHash.Value, request.Password);
            return new AuthOutcome(AuthStatus.InvalidCredentials);
        }

        // Locked-out accounts get the same answer as a wrong password, so the response never reveals
        // whether a username exists.
        if (await users.IsLockedOutAsync(user))
        {
            return new AuthOutcome(AuthStatus.InvalidCredentials);
        }

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            return new AuthOutcome(AuthStatus.InvalidCredentials);
        }

        await users.ResetAccessFailedCountAsync(user);

        // Only someone who knows the password learns that the account is disabled.
        if (user.DeletedAtUtc is not null)
        {
            return new AuthOutcome(AuthStatus.AccountPendingDeletion);
        }
        if (!user.IsActive)
        {
            return new AuthOutcome(AuthStatus.AccountDisabled);
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        await users.UpdateAsync(user);

        return await StartSessionAsync(user, client, cancellationToken);
    }

    public async Task<AuthOutcome> RefreshAsync(string? rawRefreshToken, ClientInfo client, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(rawRefreshToken))
        {
            return new AuthOutcome(AuthStatus.InvalidRefreshToken);
        }

        var rotation = await refreshTokens.RotateAsync(rawRefreshToken, client, cancellationToken);
        if (!rotation.Succeeded)
        {
            return new AuthOutcome(AuthStatus.InvalidRefreshToken);
        }

        var user = await users.FindByIdAsync(rotation.UserId!);
        if (user is null || !user.IsActive || user.DeletedAtUtc is not null)
        {
            if (user is not null)
            {
                await refreshTokens.RevokeAllForUserAsync(user.Id, cancellationToken);
            }
            return new AuthOutcome(AuthStatus.InvalidRefreshToken);
        }

        var response = await CreateResponseAsync(user);
        return new AuthOutcome(AuthStatus.Success, response, rotation.NewRawToken);
    }

    public async Task LogoutAsync(string? rawRefreshToken, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(rawRefreshToken))
        {
            await refreshTokens.RevokeFamilyOfAsync(rawRefreshToken, cancellationToken);
        }
    }

    public async Task<AuthOutcome> ChangePasswordAsync(string userId, ChangePasswordRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null)
        {
            return new AuthOutcome(AuthStatus.InvalidCredentials);
        }

        // Guessing the current password with a stolen access token counts towards lockout.
        if (await users.IsLockedOutAsync(user))
        {
            return new AuthOutcome(AuthStatus.InvalidCredentials);
        }

        var changed = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!changed.Succeeded)
        {
            if (changed.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
            {
                await users.AccessFailedAsync(user);
                return new AuthOutcome(AuthStatus.ValidationFailed, Errors: new Dictionary<string, string[]>
                {
                    ["currentPassword"] = [Lang.T("كلمة المرور الحالية غير صحيحة.", "The current password is incorrect.")],
                });
            }
            return new AuthOutcome(AuthStatus.ValidationFailed, Errors: ToErrors(changed, passwordField: "newPassword"));
        }

        // ChangePasswordAsync rotated the security stamp, so every old access token is now rejected.
        // Sign out every other device and give this one a fresh session.
        await users.ResetAccessFailedCountAsync(user);
        await refreshTokens.RevokeAllForUserAsync(user.Id, cancellationToken);
        logger.LogInformation("User {UserId} changed their password", user.Id);

        return await StartSessionAsync(user, client, cancellationToken);
    }

    private async Task<AuthOutcome> StartSessionAsync(ApplicationUser user, ClientInfo client, CancellationToken cancellationToken)
    {
        var response = await CreateResponseAsync(user);
        var refreshToken = await refreshTokens.IssueAsync(user.Id, Guid.NewGuid(), client, cancellationToken);
        return new AuthOutcome(AuthStatus.Success, response, refreshToken);
    }

    private async Task<AuthResponse> CreateResponseAsync(ApplicationUser user)
    {
        var roles = (await users.GetRolesAsync(user)).ToList();
        var (accessToken, expiresAtUtc) = tokens.CreateAccessToken(user, roles);
        return new AuthResponse(accessToken, expiresAtUtc, new AuthUser(user.Id, user.UserName ?? string.Empty, user.DisplayName, roles));
    }

    private bool IsAdminUsername(string username) =>
        !string.IsNullOrWhiteSpace(adminOptions.Value.Username)
        && string.Equals(username, adminOptions.Value.Username, StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string[]> ToErrors(IdentityResult result, string passwordField) =>
        result.Errors
            .GroupBy(e => e.Code.StartsWith("Password", StringComparison.Ordinal) || e.Code == CommonPasswordValidator.ErrorCode ? passwordField : "username")
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).Distinct().ToArray());
}
