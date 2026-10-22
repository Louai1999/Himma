using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Himma.Api.Infrastructure.Localization;

namespace Himma.Api.Infrastructure.Errors;

/// <summary>
/// Turns unhandled exceptions into RFC 7807 ProblemDetails. The exception message is only
/// shown in Development; stack traces are never sent to the client.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // The client went away; nothing useful to send back.
            logger.LogInformation("Request {Method} {Path} was cancelled by the client", httpContext.Request.Method, httpContext.Request.Path);
            httpContext.Response.StatusCode = 499;
            return true;
        }

        logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = Lang.T("حدث خطأ غير متوقع", "Something went wrong"),
            Detail = environment.IsDevelopment() ? exception.Message : Lang.T("يرجى المحاولة مرة أخرى بعد قليل.", "Please try again in a moment."),
            Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
        };

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
