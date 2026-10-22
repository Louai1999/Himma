using Himma.Api.Infrastructure.Auth;
using Himma.Api.Infrastructure.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Himma.Api.Infrastructure.Http;

/// <summary>
/// Base for every signed-in user endpoint. Every query must be filtered by <see cref="UserId"/>,
/// which comes from the validated token only, never from the URL or body.
/// </summary>
[ApiController]
[Authorize]
public abstract class HimmaControllerBase : ControllerBase
{
    protected string UserId => User.GetUserId();

    /// <summary>Also used when the item exists but belongs to someone else, so ids of other users leak nothing.</summary>
    protected ObjectResult NotFoundProblem(string? detail = null) =>
        Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: Lang.T("غير موجود", "Not found"),
            detail: detail ?? Lang.T("العنصر غير موجود.", "This item does not exist."));

    protected ActionResult InvalidField(string field, string message) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [field] = [message] })
        {
            Title = Lang.T("بعض البيانات غير صحيحة", "Some details need fixing"),
        });
}
