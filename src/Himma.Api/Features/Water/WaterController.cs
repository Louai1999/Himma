using Himma.Api.Data;
using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Http;
using Himma.Api.Infrastructure.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Features.Water;

[Route("api/water")]
public sealed class WaterController(AppDbContext db) : HimmaControllerBase
{
    private static readonly WaterReminderDto DefaultReminder = new(Enabled: false, IntervalHours: 2);

    [HttpGet("{date}")]
    public async Task<ActionResult<WaterDayResponse>> GetDay(string date, CancellationToken cancellationToken)
    {
        if (!ApiDate.TryParse(date, out var day))
        {
            return InvalidField("date", ApiDate.InvalidMessage);
        }

        var userId = UserId;
        var entries = await db.WaterEntries.AsNoTracking()
            .Where(w => w.UserId == userId && w.Date == day)
            .OrderBy(w => w.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return new WaterDayResponse(day, entries.Sum(w => w.Ml), entries.Select(WaterEntryDto.From).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<WaterEntryDto>> Create(WaterRequest request, CancellationToken cancellationToken)
    {
        var entry = new WaterEntry
        {
            UserId = UserId,
            Date = ApiDate.Parse(request.Date),
            Ml = request.Ml!.Value,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.WaterEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, WaterEntryDto.From(entry));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = UserId;
        var deleted = await db.WaterEntries
            .Where(w => w.Id == id && w.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0 ? NotFoundProblem() : NoContent();
    }

    [HttpGet("reminder")]
    public async Task<ActionResult<WaterReminderDto>> GetReminder(CancellationToken cancellationToken)
    {
        var userId = UserId;
        var setting = await db.WaterReminderSettings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        return setting is null ? DefaultReminder : new WaterReminderDto(setting.Enabled, setting.IntervalHours);
    }

    [HttpPut("reminder")]
    public async Task<ActionResult<WaterReminderDto>> PutReminder(WaterReminderRequest request, CancellationToken cancellationToken)
    {
        var userId = UserId;
        var setting = await db.WaterReminderSettings.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        if (setting is null)
        {
            setting = new WaterReminderSetting { UserId = userId };
            db.WaterReminderSettings.Add(setting);
        }

        setting.Enabled = request.Enabled!.Value;
        setting.IntervalHours = request.IntervalHours!.Value;
        await db.SaveChangesAsync(cancellationToken);

        return new WaterReminderDto(setting.Enabled, setting.IntervalHours);
    }
}
