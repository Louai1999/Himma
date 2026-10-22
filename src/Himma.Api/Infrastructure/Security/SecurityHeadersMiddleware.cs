namespace Himma.Api.Infrastructure.Security;

/// <summary>
/// Adds security headers to every response. Headers are added in OnStarting so they survive
/// the exception handler, which clears headers before writing an error response.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    // 'self' in font-src allows self-hosted fonts later; Google Fonts are allowed until then.
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data:; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    private const string PermissionsPolicy = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";

    public Task InvokeAsync(HttpContext context)
    {
        // Swagger UI uses inline scripts, so it is exempt from the CSP. It only exists in Development.
        var skipCsp = environment.IsDevelopment() && context.Request.Path.StartsWithSegments("/swagger");

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            if (!skipCsp)
            {
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
            }
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = PermissionsPolicy;
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            return Task.CompletedTask;
        });

        return next(context);
    }
}
