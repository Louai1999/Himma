using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Himma.Api.Infrastructure.Email;

/// <summary>
/// Transactional email through Brevo (free tier: 300 emails a day). Set Email__ApiKey and Email__FromAddress
/// (a sender address verified in Brevo) to turn email on; without them every email feature is switched off
/// and the app says so instead of failing.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string? ApiKey { get; set; }
    public string? FromAddress { get; set; }
    public string FromName { get; set; } = "Himma هِمّة";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(FromAddress);
}

public sealed record EmailMessage(string ToAddress, string Subject, string Text, string Html);

public interface IEmailSender
{
    bool IsEnabled { get; }

    /// <summary>False when the provider refused or could not be reached; never throws for that.</summary>
    Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public sealed class BrevoEmailSender(HttpClient http, IOptions<EmailOptions> options, ILogger<BrevoEmailSender> logger) : IEmailSender
{
    public bool IsEnabled => options.Value.IsConfigured;

    public async Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return false;
        }

        var settings = options.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email")
        {
            Content = JsonContent.Create(new
            {
                sender = new { name = settings.FromName, email = settings.FromAddress },
                to = new[] { new { email = message.ToAddress } },
                subject = message.Subject,
                textContent = message.Text,
                htmlContent = message.Html,
            }),
        };
        request.Headers.Add("api-key", settings.ApiKey);
        request.Headers.Accept.ParseAdd("application/json");

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }
            // The address and the key are never logged.
            logger.LogWarning("Email provider refused a message: {Status}", (int)response.StatusCode);
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning("Email provider could not be reached: {Error}", ex.GetType().Name);
            return false;
        }
    }
}

/// <summary>
/// Development only, when no provider is set: writes emails (codes included) to the log instead of sending,
/// so the recovery screens can be tried locally. Never registered in production.
/// </summary>
public sealed class DevelopmentLogEmailSender(ILogger<DevelopmentLogEmailSender> logger) : IEmailSender
{
    public bool IsEnabled => true;

    public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogWarning("DEV EMAIL to {To} | {Subject}\n{Text}", message.ToAddress, message.Subject, message.Text);
        return Task.FromResult(true);
    }
}
