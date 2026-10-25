using Himma.Api.Data;
using Himma.Api.Domain.Body;
using Himma.Api.Domain.Entities;
using Himma.Api.Domain.Nutrition;
using Himma.Api.Infrastructure.Http;
using Himma.Api.Infrastructure.Localization;
using Himma.Api.Infrastructure.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Features.Body;

[Route("api/body")]
public sealed class BodyController(AppDbContext db) : HimmaControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BodyResponse>> Get(CancellationToken cancellationToken)
    {
        var userId = UserId;
        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        var measurements = await db.BodyMeasurements.AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.Date)
            .ToListAsync(cancellationToken);
        var weights = await db.WeightEntries.AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderBy(w => w.Date)
            .ToListAsync(cancellationToken);

        var entries = measurements.Select(m =>
        {
            var weightKg = WeightOn(m.Date, weights, profile);
            var composition = profile is null || weightKg is null
                ? null
                : BodyCompositionCalculator.Calculate(profile.Gender, profile.HeightCm, weightKg.Value,
                    m.NeckCm, m.WaistCm, m.HipCm, m.BodyFatPercent);
            return BodyEntryDto.From(m, weightKg, composition);
        }).ToList();

        var latest = entries.LastOrDefault(e => e.Composition is not null);
        var projection = profile?.TargetBodyFatPercent is { } target && latest is { Composition: { } current, WeightKg: { } weight }
            ? BodyCompositionCalculator.Project(profile.Gender, weight, current, target)
            : null;

        return new BodyResponse(profile is not null, entries, latest, profile?.TargetBodyFatPercent, projection);
    }

    /// <summary>One entry per date: saving the same date again replaces it.</summary>
    [HttpPost]
    public async Task<ActionResult<BodyEntryDto>> Upsert(BodyMeasurementRequest request, CancellationToken cancellationToken)
    {
        var userId = UserId;
        var date = ApiDate.Parse(request.Date);

        var entry = await db.BodyMeasurements.FirstOrDefaultAsync(m => m.UserId == userId && m.Date == date, cancellationToken);
        if (entry is null)
        {
            entry = new BodyMeasurement { UserId = userId, Date = date, CreatedAtUtc = DateTime.UtcNow };
            db.BodyMeasurements.Add(entry);
        }

        entry.NeckCm = Round(request.NeckCm);
        entry.WaistCm = Round(request.WaistCm);
        entry.HipCm = Round(request.HipCm);
        entry.ChestCm = Round(request.ChestCm);
        entry.ArmCm = Round(request.ArmCm);
        entry.ThighCm = Round(request.ThighCm);
        entry.BodyFatPercent = Round(request.BodyFatPercent);
        entry.MuscleMassKg = Round(request.MuscleMassKg);
        await db.SaveChangesAsync(cancellationToken);

        return BodyEntryDto.From(entry, weightKg: null, composition: null);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = UserId;
        var deleted = await db.BodyMeasurements
            .Where(m => m.Id == id && m.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0 ? NotFoundProblem() : NoContent();
    }

    [HttpPut("goal")]
    public async Task<IActionResult> PutGoal(BodyGoalRequest request, CancellationToken cancellationToken)
    {
        var userId = UserId;
        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is null)
        {
            return NotFoundProblem(Lang.T("لم يتم إعداد الملف الشخصي بعد.", "Your profile is not set up yet."));
        }

        profile.TargetBodyFatPercent = Round(request.TargetBodyFatPercent);
        profile.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// The weight logged on that date or the closest earlier one; before the first log, the first log;
    /// with no log at all, the profile weight.
    /// </summary>
    private static double? WeightOn(DateOnly date, List<WeightEntry> weightsOldestFirst, UserProfile? profile) =>
        weightsOldestFirst.LastOrDefault(w => w.Date <= date)?.WeightKg
        ?? weightsOldestFirst.FirstOrDefault()?.WeightKg
        ?? profile?.WeightKg;

    private static double? Round(double? value) => value is { } v ? NutritionMath.Round1(v) : null;
}
