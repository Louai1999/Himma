using Microsoft.AspNetCore.Identity;

namespace Himma.Api.Infrastructure.Identity;

/// <summary>Arabic messages for the Identity errors a user can actually see.</summary>
public sealed class ArabicIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = "اسم المستخدم مستخدم. اختر اسماً آخر." };

    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = "اسم المستخدم يقبل الحروف الإنجليزية والأرقام والرموز . _ - فقط." };

    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"كلمة المرور يجب ألا تقل عن {length} أحرف." };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"كلمة المرور تحتاج {uniqueChars} أحرف مختلفة على الأقل." };

    public override IdentityError PasswordMismatch() =>
        new() { Code = nameof(PasswordMismatch), Description = "كلمة المرور غير صحيحة." };
}
