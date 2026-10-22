using Himma.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Himma.Api.Infrastructure.Auth;

/// <summary>
/// Asks for the password again before destructive actions (deleting an account, admin actions).
/// Wrong answers count towards the normal lockout, so a stolen session cannot be used to guess it.
/// </summary>
public sealed class PasswordReauthentication(UserManager<ApplicationUser> users)
{
    public async Task<bool> VerifyAsync(ApplicationUser user, string password)
    {
        if (string.IsNullOrEmpty(password) || await users.IsLockedOutAsync(user))
        {
            return false;
        }

        if (!await users.CheckPasswordAsync(user, password))
        {
            await users.AccessFailedAsync(user);
            return false;
        }

        await users.ResetAccessFailedCountAsync(user);
        return true;
    }
}
