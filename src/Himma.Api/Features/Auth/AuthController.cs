using Himma.Api.Infrastructure.Auth;
using Himma.Api.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Himma.Api.Infrastructure.Localization;

namespace Himma.Api.Features.Auth;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class AuthController(AuthService auth, RefreshCookie refreshCookie) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken) =>
        ToResult(await auth.RegisterAsync(request, Client(), cancellationToken), StatusCodes.Status201Created);

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken) =>
        ToResult(await auth.LoginAsync(request, Client(), cancellationToken));

    /// <summary>Uses the refresh cookie to issue a new access token, and rotates the cookie.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken) =>
        ToResult(await auth.RefreshAsync(refreshCookie.Read(Request), Client(), cancellationToken));

    /// <summary>Anonymous on purpose: logging out must work even when the access token has expired.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(refreshCookie.Read(Request), cancellationToken);
        refreshCookie.Delete(Response);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken) =>
        ToResult(await auth.ChangePasswordAsync(User.GetUserId(), request, Client(), cancellationToken));

    private ClientInfo Client() =>
        new(HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());

    private IActionResult ToResult(AuthOutcome outcome, int successStatus = StatusCodes.Status200OK)
    {
        switch (outcome.Status)
        {
            case AuthStatus.Success:
                refreshCookie.Write(Response, outcome.RefreshToken!);
                return StatusCode(successStatus, outcome.Response);

            case AuthStatus.InvalidCredentials:
                return Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: Lang.T("تعذّر تسجيل الدخول", "Could not sign in"),
                    detail: Lang.T("اسم المستخدم أو كلمة المرور غير صحيحة، أو أن الدخول أُوقف مؤقتاً بعد محاولات كثيرة. حاول مرة أخرى بعد قليل.", "The username or password is incorrect, or sign-in is paused for a while after too many attempts. Please try again shortly."));

            case AuthStatus.AccountDisabled:
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: Lang.T("الحساب غير مفعّل", "Account not active"),
                    detail: Lang.T("هذا الحساب موقوف حالياً. تواصل مع الدعم إذا كنت تظن أن هذا خطأ.", "This account is currently suspended. Contact support if you think this is a mistake."));

            case AuthStatus.UsernameTaken:
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: Lang.T("اسم المستخدم مستخدم", "Username taken"),
                    detail: Lang.T("اختر اسم مستخدم آخر.", "Please choose another username."));

            case AuthStatus.ValidationFailed:
                return ValidationProblem(new ValidationProblemDetails(outcome.Errors ?? new Dictionary<string, string[]>())
                {
                    Title = Lang.T("بعض البيانات غير صحيحة", "Some details need fixing"),
                });

            case AuthStatus.InvalidRefreshToken:
                refreshCookie.Delete(Response);
                return Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: Lang.T("انتهت الجلسة", "Session ended"),
                    detail: Lang.T("سجّل الدخول مرة أخرى.", "Please sign in again."));

            default:
                throw new InvalidOperationException($"Unhandled auth status {outcome.Status}.");
        }
    }
}
