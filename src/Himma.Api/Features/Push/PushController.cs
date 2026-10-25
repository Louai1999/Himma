using FluentValidation;
using Himma.Api.Data;
using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Http;
using Himma.Api.Infrastructure.Localization;
using Himma.Api.Infrastructure.Push;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Features.Push;

public sealed class PushSubscriptionRequest
{
    public string Endpoint { get; init; } = string.Empty;
    public string P256dh { get; init; } = string.Empty;
    public string Auth { get; init; } = string.Empty;
    public string TimeZone { get; init; } = string.Empty;
    public string Language { get; init; } = Lang.Arabic;
}

public sealed class PushUnsubscribeRequest
{
    public string Endpoint { get; init; } = string.Empty;
}

public sealed record PushKeyResponse(string PublicKey);

public sealed record PushStatusResponse(int Devices);

public sealed record PushTestResponse(int Sent);

[Route("api/push")]
public sealed class PushController(AppDbContext db, PushKeyStore keys, IPushSender sender) : HimmaControllerBase
{
    public const int MaxDevicesPerUser = 10;

    /// <summary>The public key the browser needs to subscribe. It is public by design.</summary>
    [HttpGet("key")]
    public PushKeyResponse Key() => new(keys.Keys.PublicKey);

    [HttpGet("devices")]
    public async Task<PushStatusResponse> Devices(CancellationToken cancellationToken)
    {
        var userId = UserId;
        return new PushStatusResponse(await db.PushDevices.CountAsync(d => d.UserId == userId, cancellationToken));
    }

    /// <summary>
    /// Saves this device for reminders. The same endpoint again updates it; an endpoint saved by another
    /// account moves to this one (the browser now belongs to whoever is signed in).
    /// </summary>
    [HttpPost("subscriptions")]
    public async Task<IActionResult> Subscribe(PushSubscriptionRequest request, CancellationToken cancellationToken)
    {
        var userId = UserId;
        var device = await db.PushDevices.FirstOrDefaultAsync(d => d.Endpoint == request.Endpoint, cancellationToken);
        if (device is null || device.UserId != userId)
        {
            var count = await db.PushDevices.CountAsync(d => d.UserId == userId, cancellationToken);
            if (count >= MaxDevicesPerUser)
            {
                // Make room by forgetting this user's oldest device.
                await db.PushDevices.Where(d => d.UserId == userId).OrderBy(d => d.CreatedAtUtc).Take(1).ExecuteDeleteAsync(cancellationToken);
            }
        }

        if (device is null)
        {
            device = new PushDevice { Endpoint = request.Endpoint };
            db.PushDevices.Add(device);
        }
        device.UserId = userId;
        device.P256dh = request.P256dh;
        device.Auth = request.Auth;
        device.TimeZone = request.TimeZone;
        device.Language = request.Language;
        device.CreatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Forgets this device. Only the signed-in user's own devices can be removed.</summary>
    [HttpPost("subscriptions/delete")]
    public async Task<IActionResult> Unsubscribe(PushUnsubscribeRequest request, CancellationToken cancellationToken)
    {
        var userId = UserId;
        await db.PushDevices.Where(d => d.UserId == userId && d.Endpoint == request.Endpoint).ExecuteDeleteAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Sends a test notification to every device of the signed-in user.</summary>
    [HttpPost("test")]
    public async Task<PushTestResponse> Test(CancellationToken cancellationToken)
    {
        var userId = UserId;
        var devices = await db.PushDevices.Where(d => d.UserId == userId).ToListAsync(cancellationToken);
        var payload = new PushPayload(
            Lang.T("هِمّة", "Himma"),
            Lang.T("التنبيهات تعمل على هذا الجهاز ✅", "Notifications work on this device ✅"),
            "himma-test",
            "/#today");

        var sent = 0;
        foreach (var device in devices)
        {
            var result = await sender.SendAsync(device, payload, cancellationToken);
            if (result == PushSendResult.Sent)
            {
                sent++;
            }
            else if (result == PushSendResult.Gone)
            {
                db.PushDevices.Remove(device);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        return new PushTestResponse(sent);
    }
}

public sealed class PushSubscriptionRequestValidator : AbstractValidator<PushSubscriptionRequest>
{
    /// <summary>
    /// The server sends requests to the endpoint, so only real browser push services are accepted;
    /// anything else could be used to make the server call internal or third-party addresses.
    /// </summary>
    private static readonly string[] PushServiceHosts =
    [
        ".googleapis.com",        // Chrome, Edge on Android, most Android browsers (FCM)
        ".push.services.mozilla.com",
        ".push.apple.com",        // Safari and installed web apps on iPhone and Mac
        ".notify.windows.com",    // Edge on Windows (WNS)
    ];

    public PushSubscriptionRequestValidator()
    {
        RuleFor(x => x.Endpoint).NotEmpty().MaximumLength(600).Must(BePushServiceUrl)
            .WithMessage(_ => Lang.T("عنوان الإشعارات غير مدعوم.", "This notification address is not supported."));
        RuleFor(x => x.P256dh).NotEmpty().MaximumLength(200).Matches("^[A-Za-z0-9_-]+=*$");
        RuleFor(x => x.Auth).NotEmpty().MaximumLength(100).Matches("^[A-Za-z0-9_-]+=*$");
        RuleFor(x => x.TimeZone).NotEmpty().MaximumLength(64).Must(BeTimeZone)
            .WithMessage(_ => Lang.T("المنطقة الزمنية غير معروفة.", "Unknown time zone."));
        RuleFor(x => x.Language).Must(l => l is Lang.Arabic or Lang.English);
    }

    public static bool BePushServiceUrl(string endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && string.IsNullOrEmpty(uri.UserInfo)
        && PushServiceHosts.Any(suffix => ("." + uri.IdnHost).EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

    private static bool BeTimeZone(string id) => TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);
}
