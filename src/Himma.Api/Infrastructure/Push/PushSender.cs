using System.Net;
using System.Text.Json;
using Himma.Api.Domain.Entities;
using Himma.Api.Options;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Options;

namespace Himma.Api.Infrastructure.Push;

/// <summary>
/// What the service worker shows. TakeToken adds a "Taken" button (TakeLabel) that logs the dose without
/// opening the app. Tag replaces an older notification with the same tag instead of stacking.
/// </summary>
public sealed record PushPayload(string Title, string Body, string Tag, string Url, string? TakeToken = null, string? TakeLabel = null);

public enum PushSendResult { Sent, Gone, Failed }

public interface IPushSender
{
    Task<PushSendResult> SendAsync(PushDevice device, PushPayload payload, CancellationToken cancellationToken);
}

/// <summary>Sends through the browser's push service (Google, Mozilla, Apple, Microsoft), signed with the VAPID keys.</summary>
public sealed class WebPushSender(HttpClient httpClient, PushKeyStore keyStore, IOptions<PushOptions> options, ILogger<WebPushSender> logger)
    : IPushSender
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<PushSendResult> SendAsync(PushDevice device, PushPayload payload, CancellationToken cancellationToken)
    {
        var keys = keyStore.Keys;
        var client = new PushServiceClient(httpClient)
        {
            DefaultAuthentication = new VapidAuthentication(keys.PublicKey, keys.PrivateKey) { Subject = options.Value.Subject },
        };
        var subscription = new PushSubscription
        {
            Endpoint = device.Endpoint,
            Keys = new Dictionary<string, string> { ["p256dh"] = device.P256dh, ["auth"] = device.Auth },
        };
        var message = new PushMessage(JsonSerializer.Serialize(payload, Json))
        {
            // A reminder that arrives hours late is noise, so push services may drop it after an hour.
            TimeToLive = 3600,
            Urgency = PushMessageUrgency.High,
        };

        try
        {
            await client.RequestPushMessageDeliveryAsync(subscription, message, cancellationToken);
            return PushSendResult.Sent;
        }
        catch (PushServiceClientException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return PushSendResult.Gone; // the user turned notifications off or uninstalled; forget the device
        }
        catch (Exception ex) when (ex is PushServiceClientException or HttpRequestException or TaskCanceledException)
        {
            // Only the error type and status: the endpoint (which may appear in messages) works like a
            // password for sending to that device, so it is never logged.
            logger.LogWarning("Push to device {DeviceId} failed: {Error} {Status}",
                device.Id, ex.GetType().Name, (ex as PushServiceClientException)?.StatusCode);
            return PushSendResult.Failed;
        }
    }
}
