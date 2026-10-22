using Himma.Api.Infrastructure.Localization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;

namespace Himma.Api.Infrastructure.Http;

public static class HostingSetup
{
    /// <summary>Requests are small JSON documents; anything bigger is refused early.</summary>
    public const long MaxRequestBodyBytes = 256 * 1024;

    public static WebApplicationBuilder AddHimmaHosting(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = MaxRequestBodyBytes);

        // Render and Railway tell the app which port to listen on through PORT.
        var port = builder.Configuration["PORT"];
        if (int.TryParse(port, out var portNumber) && portNumber is > 0 and < 65536)
        {
            builder.WebHost.UseUrls($"http://0.0.0.0:{portNumber}");
        }

        // Hosting platforms (Render, Railway, Azure App Service) put a proxy in front of the app.
        // Trust the one X-Forwarded-For / X-Forwarded-Proto hop it adds, so HTTPS redirection and the
        // per-IP rate limits see the real client. Enable only when the app is reachable through that proxy alone.
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        // JSON that cannot be read (wrong type, unknown field, broken syntax) gets a short message per field
        // instead of the serializer's text, which would reveal internal class names.
        builder.Services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .GroupBy(entry => FieldName(entry.Key))
                    .ToDictionary(
                        group => group.Key,
                        _ => new[] { Lang.T("قيمة غير صالحة أو حقل غير معروف.", "Invalid value or unknown field.") });

                return new ObjectResult(new ValidationProblemDetails(errors)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = Lang.T("بعض البيانات غير صحيحة", "Some details need fixing"),
                })
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    ContentTypes = { "application/problem+json" },
                };
            };
        });

        return builder;
    }

    /// <summary>"$.role" becomes "role"; errors about the whole body (key "" or "$") become "body".</summary>
    private static string FieldName(string key)
    {
        var name = key.TrimStart('$', '.');
        return name.Length == 0 ? "body" : name;
    }

    public static WebApplication UseHimmaForwardedHeaders(this WebApplication app)
    {
        if (app.Configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
        {
            app.UseForwardedHeaders();
        }
        return app;
    }
}
