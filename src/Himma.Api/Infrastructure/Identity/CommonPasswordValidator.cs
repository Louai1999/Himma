using Himma.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Himma.Api.Infrastructure.Identity;

/// <summary>
/// Blocks passwords that attackers try first: well-known leaked passwords, a single repeated
/// character, simple keyboard/number runs, and passwords that contain the username.
/// </summary>
public sealed class CommonPasswordValidator : IPasswordValidator<ApplicationUser>
{
    public const string ErrorCode = "CommonPassword";

    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password1", "password12", "password123", "password1234", "passw0rd", "p@ssw0rd", "p@ssword",
        "12345678", "123456789", "1234567890", "0123456789", "87654321", "987654321", "12341234", "11223344",
        "123123123", "12121212", "11111111", "00000000", "88888888", "99999999", "55555555", "66666666",
        "qwertyui", "qwertyuiop", "qwerty123", "qwerty12", "qwer1234", "q1w2e3r4", "1q2w3e4r", "1qaz2wsx",
        "asdfghjk", "asdfghjkl", "zxcvbnm1", "zxcvbnm123", "aa123456", "abc12345", "abcd1234", "abcdefgh",
        "iloveyou", "iloveyou1", "letmein1", "welcome1", "welcome123", "admin123", "admin1234", "administrator",
        "football", "baseball", "sunshine", "princess", "superman", "starwars", "trustno1", "whatever",
        "computer", "freedom1", "changeme", "dragon12", "master12", "monkey12", "michael1", "jennifer",
        "himma123", "himma1234", "fitness1", "fitness123", "mohammed", "mohammad", "muhammad", "mohamed1",
        "abdullah", "alhamdulillah", "bismillah", "saudi123", "kuwait123", "qatar123", "dubai123", "oman1234",
    };

    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            // Length rules report empty passwords.
            return Task.FromResult(IdentityResult.Success);
        }

        var containsUsername = user.UserName is { Length: >= 3 } username
            && password.Contains(username, StringComparison.OrdinalIgnoreCase);

        var isWeak = CommonPasswords.Contains(password)
            || password.Distinct().Count() == 1
            || IsSequential(password)
            || containsUsername;

        return Task.FromResult(isWeak
            ? IdentityResult.Failed(new IdentityError
            {
                Code = ErrorCode,
                Description = "كلمة المرور سهلة التخمين. اختر كلمة أطول وأقل شيوعاً، ولا تضع فيها اسم المستخدم.",
            })
            : IdentityResult.Success);
    }

    /// <summary>True for runs like "abcdefgh", "23456789" or "98765432".</summary>
    private static bool IsSequential(string password)
    {
        var ascending = true;
        var descending = true;
        for (var i = 1; i < password.Length; i++)
        {
            var step = char.ToLowerInvariant(password[i]) - char.ToLowerInvariant(password[i - 1]);
            ascending &= step == 1;
            descending &= step == -1;
        }
        return ascending || descending;
    }
}
