using Microsoft.AspNetCore.Identity;
using Himma.Api.Infrastructure.Localization;

namespace Himma.Api.Infrastructure.Identity;

/// <summary>Arabic / English messages for the Identity errors a user can actually see.</summary>
public sealed class LocalizedIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = Lang.T("اسم المستخدم مستخدم. اختر اسماً آخر.", "That username is taken. Please choose another.") };

    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = Lang.T("اسم المستخدم يقبل الحروف الإنجليزية والأرقام والرموز . _ - فقط.", "Username may only contain English letters, digits and . _ -") };

    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = Lang.T($"كلمة المرور يجب ألا تقل عن {length} أحرف.", $"Password must be at least {length} characters.") };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        new() { Code = nameof(PasswordRequiresUniqueChars), Description = Lang.T($"كلمة المرور تحتاج {uniqueChars} أحرف مختلفة على الأقل.", $"Password needs at least {uniqueChars} different characters.") };

    public override IdentityError PasswordMismatch() =>
        new() { Code = nameof(PasswordMismatch), Description = Lang.T("كلمة المرور غير صحيحة.", "The password is incorrect.") };
}
