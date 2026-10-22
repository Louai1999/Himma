using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Http;
using Himma.Api.Infrastructure.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Himma.Api.Features.Admin;

/// <summary>Every /api/admin endpoint requires the Admin role; other signed-in users get 403.</summary>
[Authorize(Roles = Roles.Admin)]
public abstract class AdminControllerBase(UserManager<ApplicationUser> users) : HimmaControllerBase
{
    protected UserManager<ApplicationUser> Users { get; } = users;

    /// <summary>
    /// Loads a subscriber the admin may manage. Admin accounts (including the admin's own) are never
    /// a valid target, which also guarantees the last admin can never be removed.
    /// </summary>
    protected async Task<(ApplicationUser? User, ActionResult? Error)> FindManageableAsync(string id)
    {
        var user = await Users.FindByIdAsync(id);
        if (user is null)
        {
            return (null, NotFoundProblem());
        }

        if (user.Id == UserId || await Users.IsInRoleAsync(user, Roles.Admin))
        {
            return (null, Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: Lang.T("غير مسموح", "Not allowed"),
                detail: Lang.T("لا يمكن تنفيذ هذا الإجراء على حسابك أو على حساب مدير آخر.", "This action cannot be used on your own account or on another admin.")));
        }

        return (user, null);
    }
}
