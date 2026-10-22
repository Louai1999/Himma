using Himma.Api.Infrastructure.Auth;
using Himma.Api.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
                    title: "تعذّر تسجيل الدخول",
                    detail: "اسم المستخدم أو كلمة المرور غير صحيحة، أو أن الدخول أُوقف مؤقتاً بعد محاولات كثيرة. حاول مرة أخرى بعد قليل.");

            case AuthStatus.AccountDisabled:
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "الحساب غير مفعّل",
                    detail: "هذا الحساب موقوف حالياً. تواصل مع الدعم إذا كنت تظن أن هذا خطأ.");

            case AuthStatus.UsernameTaken:
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "اسم المستخدم مستخدم",
                    detail: "اختر اسم مستخدم آخر.");

            case AuthStatus.ValidationFailed:
                return ValidationProblem(new ValidationProblemDetails(outcome.Errors ?? new Dictionary<string, string[]>())
                {
                    Title = "بعض البيانات غير صحيحة",
                });

            case AuthStatus.InvalidRefreshToken:
                refreshCookie.Delete(Response);
                return Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "انتهت الجلسة",
                    detail: "سجّل الدخول مرة أخرى.");

            default:
                throw new InvalidOperationException($"Unhandled auth status {outcome.Status}.");
        }
    }
}
