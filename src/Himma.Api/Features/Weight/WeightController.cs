using Himma.Api.Data;
using Himma.Api.Domain.Entities;
using Himma.Api.Domain.Nutrition;
using Himma.Api.Infrastructure.Http;
using Himma.Api.Infrastructure.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Features.Weight;

[Route("api/weight")]
public sealed class WeightController(AppDbContext db) : HimmaControllerBase
{
    [HttpGet]
    public async Task<ActionResult<WeightHistoryResponse>> Get(CancellationToken cancellationToken)
    {
        var userId = UserId;
        var entries = await db.WeightEntries.AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderBy(w => w.Date)
            .ToListAsync(cancellationToken);
        var targetWeight = await db.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.TargetWeightKg)
            .FirstOrDefaultAsync(cancellationToken);

        return new WeightHistoryResponse(
            entries.Select(WeightEntryDto.From).ToList(),
            entries.FirstOrDefault()?.WeightKg,
            entries.LastOrDefault()?.WeightKg,
            targetWeight);
    }

    /// <summary>
    /// One entry per date: saving the same date again replaces it. The profile weight (and so the targets)
    /// follows the newest entry only, so back-filling an old date does not change today's targets.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<WeightEntryDto>> Upsert(WeightRequest request, CancellationToken cancellationToken)
    {
        var userId = UserId;
        var date = ApiDate.Parse(request.Date);
        var weightKg = NutritionMath.Round1(request.WeightKg!.Value);

        var entry = await db.WeightEntries.FirstOrDefaultAsync(w => w.UserId == userId && w.Date == date, cancellationToken);
        if (entry is null)
        {
            entry = new WeightEntry { UserId = userId, Date = date, CreatedAtUtc = DateTime.UtcNow };
            db.WeightEntries.Add(entry);
        }
        entry.WeightKg = weightKg;

        var hasNewerEntry = await db.WeightEntries.AnyAsync(w => w.UserId == userId && w.Date > date, cancellationToken);
        if (!hasNewerEntry)
        {
            var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
            if (profile is not null)
            {
                profile.WeightKg = weightKg;
                profile.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return WeightEntryDto.From(entry);
    }
}
