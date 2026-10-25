using System.Globalization;
using Himma.Api.Data;
using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Domain.Supplements;
using Himma.Api.Features.Supplements;
using Himma.Api.Infrastructure.Localization;
using Himma.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Himma.Api.Infrastructure.Push;

/// <summary>
/// Every minute, sends the reminders whose local time fell in the last minute, to every device in its own
/// time zone and language: a supplement at each of its times, a second one when a dose is still not logged
/// MissedAfterMinutes later, and water every N hours for users who turned the water reminder on.
/// A dose that is already logged is never reminded.
/// </summary>
public sealed class ReminderService(
    IServiceScopeFactory scopeFactory,
    IOptions<ReminderOptions> options,
    TakeTokens takeTokens,
    ILogger<ReminderService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        var last = DateTime.UtcNow;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var now = DateTime.UtcNow;
            try
            {
                await RunAsync(last, now, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Sending reminders failed");
            }
            last = now;
        }
    }

    /// <summary>Sends the reminders due in (fromUtc, toUtc]. Returns how many were sent.</summary>
    public async Task<int> RunAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IPushSender>();

        var devices = await db.PushDevices.ToListAsync(cancellationToken);
        if (devices.Count == 0)
        {
            return 0;
        }

        var userIds = devices.Select(d => d.UserId).Distinct().ToList();
        var activeUsers = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id) && u.IsActive && u.DeletedAtUtc == null)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        var supplements = await db.Supplements.AsNoTracking()
            .Where(s => activeUsers.Contains(s.UserId) && s.Times != "")
            .ToListAsync(cancellationToken);
        var water = await db.WaterReminderSettings.AsNoTracking()
            .Where(w => activeUsers.Contains(w.UserId) && w.Enabled)
            .ToDictionaryAsync(w => w.UserId, w => w.IntervalHours, cancellationToken);
        // Local dates can be a day either side of the UTC date.
        var earliest = DateOnly.FromDateTime(fromUtc).AddDays(-1);
        var taken = await db.SupplementIntakes.AsNoTracking()
            .Where(i => activeUsers.Contains(i.UserId) && i.Date >= earliest)
            .GroupBy(i => new { i.SupplementId, i.Date })
            .Select(g => new { g.Key.SupplementId, g.Key.Date, Count = g.Count() })
            .ToDictionaryAsync(g => (g.SupplementId, g.Date), g => g.Count, cancellationToken);

        var sent = 0;
        foreach (var device in devices.Where(d => activeUsers.Contains(d.UserId)))
        {
            var zone = TimeZoneInfo.TryFindSystemTimeZoneById(device.TimeZone, out var found) ? found : TimeZoneInfo.Utc;
            var from = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, zone);
            var to = TimeZoneInfo.ConvertTimeFromUtc(toUtc, zone);
            var payloads = new List<PushPayload>();

            WithLanguage(device.Language, () =>
            {
                foreach (var supplement in supplements.Where(s => s.UserId == device.UserId))
                {
                    payloads.AddRange(SupplementReminders(supplement, from, to, taken));
                }
                if (water.TryGetValue(device.UserId, out var everyHours))
                {
                    payloads.AddRange(WaterReminders(everyHours, from, to));
                }
            });

            foreach (var payload in payloads)
            {
                var result = await sender.SendAsync(device, payload, cancellationToken);
                if (result == PushSendResult.Sent)
                {
                    sent++;
                }
                else if (result == PushSendResult.Gone)
                {
                    db.PushDevices.Remove(device);
                    break;
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return sent;
    }

    private IEnumerable<PushPayload> SupplementReminders(
        Supplement supplement, DateTime from, DateTime to, IReadOnlyDictionary<(int, DateOnly), int> taken)
    {
        var times = SupplementSchedule.ParseTimes(supplement.Times);
        var missedAfter = TimeSpan.FromMinutes(options.Value.MissedAfterMinutes);

        foreach (var date in LocalDates(from, to, extraDayBefore: true))
        {
            if (!SupplementSchedule.IsScheduledOn(supplement, date))
            {
                continue;
            }

            var takenThatDay = taken.GetValueOrDefault((supplement.Id, date));
            for (var dose = 1; dose <= times.Count; dose++)
            {
                if (takenThatDay >= dose)
                {
                    continue; // this dose (or a later one) is already logged
                }

                var at = date.ToDateTime(times[dose - 1]);
                var tag = $"supplement-{supplement.Id}-{date:yyyyMMdd}-{dose}";
                var token = takeTokens.Create(supplement.UserId, supplement.Id, date);
                if (at > from && at <= to)
                {
                    yield return new PushPayload(
                        Lang.T($"وقت {supplement.Name} 💊", $"Time for {supplement.Name} 💊"),
                        DoseText(supplement),
                        tag, "/#today", token, Lang.T("أخذتها ✅", "Taken ✅"));
                }
                else if (at + missedAfter > from && at + missedAfter <= to)
                {
                    yield return new PushPayload(
                        Lang.T($"ما سجّلت {supplement.Name} بعد", $"{supplement.Name} not logged yet"),
                        Lang.T("إذا أخذتها، اضغط «أخذتها».", "If you took it, tap “Taken”."),
                        tag, "/#today", token, Lang.T("أخذتها ✅", "Taken ✅"));
                }
            }
        }
    }

    private IEnumerable<PushPayload> WaterReminders(int everyHours, DateTime from, DateTime to)
    {
        var first = options.Value.WaterFirstHour;
        foreach (var date in LocalDates(from, to, extraDayBefore: false))
        {
            for (var hour = first; hour < options.Value.WaterLastHour; hour += Math.Max(1, everyHours))
            {
                var at = date.ToDateTime(new TimeOnly(hour, 0));
                if (at > from && at <= to)
                {
                    yield return new PushPayload(
                        Lang.T("وقت كوب ماء 💧", "Time for a glass of water 💧"),
                        Lang.T("جسمك يشكرك.", "Your body thanks you."),
                        "water", "/#today");
                }
            }
        }
    }

    /// <summary>The local dates the window touches; the day before too, for "missed" reminders after midnight.</summary>
    private static IEnumerable<DateOnly> LocalDates(DateTime from, DateTime to, bool extraDayBefore)
    {
        var start = DateOnly.FromDateTime(from).AddDays(extraDayBefore ? -1 : 0);
        for (var date = start; date <= DateOnly.FromDateTime(to); date = date.AddDays(1))
        {
            yield return date;
        }
    }

    private static string DoseText(Supplement supplement)
    {
        var dose = supplement.Dose.ToString("0.##", CultureInfo.InvariantCulture);
        var unit = supplement.Unit switch
        {
            SupplementUnit.Gram => Lang.T("غ", "g"),
            SupplementUnit.Scoop => Lang.T("سكوب", "scoop"),
            SupplementUnit.Capsule => Lang.T("كبسولة", "capsule"),
            SupplementUnit.Tablet => Lang.T("حبة", "tablet"),
            _ => Lang.T("مل", "ml"),
        };
        return string.IsNullOrWhiteSpace(supplement.Notes) ? $"{dose} {unit}" : $"{dose} {unit} · {supplement.Notes}";
    }

    /// <summary>Lang.T follows the current UI culture, so switch it to the device's language while building texts.</summary>
    private static void WithLanguage(string language, Action build)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(language == Lang.English ? Lang.English : Lang.Arabic);
        try
        {
            build();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
