using Himma.Api.Infrastructure.Http;
using Himma.Api.Infrastructure.Validation;
using Microsoft.AspNetCore.Mvc;
using Himma.Api.Infrastructure.Localization;

namespace Himma.Api.Features.History;

[Route("api/history")]
public sealed class HistoryController(HistoryService history) : HimmaControllerBase
{
    private const int MaxRangeDays = 366;
    private const int MaxSummaryDays = 90;

    /// <summary>Per-day totals for days that have data. Defaults to the last 30 days.</summary>
    [HttpGet("days")]
    public async Task<ActionResult<IReadOnlyList<HistoryDayDto>>> Days(
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        var toDate = ApiDate.UtcToday;
        if (to is not null && !ApiDate.TryParse(to, out toDate))
        {
            return InvalidField("to", ApiDate.InvalidMessage);
        }

        var fromDate = toDate.AddDays(-29);
        if (from is not null && !ApiDate.TryParse(from, out fromDate))
        {
            return InvalidField("from", ApiDate.InvalidMessage);
        }

        if (fromDate > toDate || toDate.DayNumber - fromDate.DayNumber >= MaxRangeDays)
        {
            return InvalidField("from", Lang.T($"الفترة يجب أن تكون بين يوم و {MaxRangeDays} يوماً.", $"The range must be between 1 and {MaxRangeDays} days."));
        }

        var totals = await history.GetDayTotalsAsync(UserId, fromDate, toDate, cancellationToken);
        return totals.Select(HistoryDayDto.From).ToList();
    }

    /// <summary>
    /// Adherence, streak, daily bars and weight points for the last <paramref name="days"/> days.
    /// <paramref name="today"/> is the user's local date (the streak ends today or yesterday).
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<HistorySummaryResponse>> Summary(
        [FromQuery] int days = 30,
        [FromQuery] string? today = null,
        CancellationToken cancellationToken = default)
    {
        if (days is < 1 or > MaxSummaryDays)
        {
            return InvalidField("days", Lang.T($"عدد الأيام بين 1 و {MaxSummaryDays}.", $"Days must be between 1 and {MaxSummaryDays}."));
        }

        var todayDate = ApiDate.UtcToday;
        if (today is not null && (!ApiDate.TryParse(today, out todayDate) || !ApiDate.IsPlausibleToday(todayDate)))
        {
            return InvalidField("today", Lang.T("تاريخ اليوم غير صحيح.", "Today's date is not valid."));
        }

        return await history.GetSummaryAsync(UserId, todayDate, days, cancellationToken);
    }
}
