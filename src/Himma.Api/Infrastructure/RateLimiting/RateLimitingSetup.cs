using System.Security.Claims;
using System.Threading.RateLimiting;
using Himma.Api.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Himma.Api.Infrastructure.RateLimiting;

/// <summary>Names for [EnableRateLimiting(...)] on controllers.</summary>
public static class RateLimitPolicies
{
    public const string Auth = "auth";
    public const string Ai = "ai";
}

public static class RateLimitingSetup
{
    /// <summary>
    /// Global limiter for every /api request (per user, or per IP when anonymous),
    /// plus stricter named policies for auth (per IP) and AI (per user) endpoints.
    /// </summary>
    public static IServiceCollection AddHimmaRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var limits = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new RateLimitingOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectionAsync;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.Request.Path.StartsWithSegments("/api")
                    ? RateLimitPartition.GetFixedWindowLimiter("api:" + UserOrIpKey(context), _ => FixedWindow(limits.Api))
                    : RateLimitPartition.GetNoLimiter("unlimited"));

            options.AddPolicy(RateLimitPolicies.Auth, context =>
                RateLimitPartition.GetFixedWindowLimiter("auth:" + IpKey(context), _ => FixedWindow(limits.Auth)));

            options.AddPolicy(RateLimitPolicies.Ai, context =>
                RateLimitPartition.GetFixedWindowLimiter("ai:" + UserOrIpKey(context), _ => FixedWindow(limits.Ai)));
        });

        return services;
    }

    private static FixedWindowRateLimiterOptions FixedWindow(RateLimitRule rule) => new()
    {
        PermitLimit = rule.PermitLimit,
        Window = TimeSpan.FromSeconds(rule.WindowSeconds),
        QueueLimit = 0,
        AutoReplenishment = true,
    };

    private static string IpKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";

    private static string UserOrIpKey(HttpContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
        return userId is null ? "ip:" + IpKey(context) : "user:" + userId;
    }

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }

        var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "طلبات كثيرة",
                Detail = "أرسلت طلبات كثيرة خلال وقت قصير. خذ نفساً وحاول مرة أخرى بعد قليل.",
            },
        });
    }
}
