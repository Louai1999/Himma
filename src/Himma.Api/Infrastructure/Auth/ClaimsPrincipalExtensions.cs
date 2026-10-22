using System.Security.Claims;

namespace Himma.Api.Infrastructure.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The signed-in user's id, taken only from the validated token, never from the request.</summary>
    public static string GetUserId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(HimmaClaims.Subject)
        ?? throw new InvalidOperationException("No user id in the token. Is the endpoint missing [Authorize]?");
}
