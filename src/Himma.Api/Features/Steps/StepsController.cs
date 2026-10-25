using FluentValidation;
using Himma.Api.Data;
using Himma.Api.Domain.Entities;
using Himma.Api.Domain.Walking;
using Himma.Api.Infrastructure.Http;
using Himma.Api.Infrastructure.Localization;
using Himma.Api.Infrastructure.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Features.Steps;

public sealed class StepsRequest
{
    public string Date { get; init; } = string.Empty;
    public int? Steps { get; init; }
}

/// <summary>DistanceKm and Calories are null until the profile (height, weight) is set.</summary>
public sealed record StepEntryDto(int Id, DateOnly Date, int Steps, double? DistanceKm, double? Calories);

/// <summary>Entries oldest first for the last <see cref="StepsController.HistoryDays"/> days.</summary>
public sealed record StepsResponse(bool HasProfile, double? StepLengthM, IReadOnlyList<StepEntryDto> Entries, int? WeekAverage);

public sealed class StepsRequestValidator : AbstractValidator<StepsRequest>
{
    public StepsRequestValidator()
    {
        RuleFor(x => x.Date).MustBeApiDate();
        RuleFor(x => x.Steps).NotNull().WithMessage(_ => Lang.T("عدد الخطوات مطلوب.", "Steps are required."))
            .InclusiveBetween(0, 100_000).WithMessage(_ => Lang.T("الخطوات بين 0 و 100,000.", "Steps must be between 0 and 100,000."));
    }
}

[Route("api/steps")]
public sealed class StepsController(AppDbContext db) : HimmaControllerBase
{
    public const int HistoryDays = 30;

    [HttpGet]
    public async Task<ActionResult<StepsResponse>> Get([FromQuery] string? today, CancellationToken cancellationToken)
    {
        var end = ApiDate.TryParse(today, out var parsed) ? parsed : ApiDate.UtcToday;
        var start = end.AddDays(-(HistoryDays - 1));
        var userId = UserId;
        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        var entries = await db.StepEntries.AsNoTracking()
            .Where(s => s.UserId == userId && s.Date >= start && s.Date <= end)
            .OrderBy(s => s.Date)
            .ToListAsync(cancellationToken);

        double? stepLength = profile is null ? null : WalkingCalculator.StepLengthM(profile.Gender, profile.HeightCm);
        var dtos = entries.Select(e => new StepEntryDto(
            e.Id, e.Date, e.Steps,
            stepLength is { } length ? WalkingCalculator.DistanceKm(e.Steps, length) : null,
            profile is null ? null : WalkingCalculator.CaloriesForSteps(profile.Gender, profile.HeightCm, profile.WeightKg, e.Steps)))
            .ToList();

        var lastWeek = entries.Where(e => e.Date > end.AddDays(-7)).ToList();
        int? weekAverage = lastWeek.Count == 0 ? null : (int)Math.Round(lastWeek.Average(e => e.Steps));
        return new StepsResponse(profile is not null, stepLength is { } l ? Math.Round(l, 2) : null, dtos, weekAverage);
    }

    /// <summary>One entry per date: saving the same date again replaces it.</summary>
    [HttpPost]
    public async Task<IActionResult> Upsert(StepsRequest request, CancellationToken cancellationToken)
    {
        var userId = UserId;
        var date = ApiDate.Parse(request.Date);
        var entry = await db.StepEntries.FirstOrDefaultAsync(s => s.UserId == userId && s.Date == date, cancellationToken);
        if (entry is null)
        {
            entry = new StepEntry { UserId = userId, Date = date, CreatedAtUtc = DateTime.UtcNow };
            db.StepEntries.Add(entry);
        }
        entry.Steps = request.Steps!.Value;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = UserId;
        var deleted = await db.StepEntries.Where(s => s.Id == id && s.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        return deleted == 0 ? NotFoundProblem() : NoContent();
    }

    /// <summary>"How much should I walk?": distance, steps and minutes to burn the given calories.</summary>
    [HttpGet("plan")]
    public async Task<ActionResult<WalkingPlan>> Plan([FromQuery] double? calories, CancellationToken cancellationToken)
    {
        if (calories is not (>= 10 and <= 3000))
        {
            return InvalidField("calories", Lang.T("السعرات بين 10 و 3000.", "Calories must be between 10 and 3000."));
        }

        var userId = UserId;
        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        return profile is null
            ? NotFoundProblem(Lang.T("لم يتم إعداد الملف الشخصي بعد.", "Your profile is not set up yet."))
            : WalkingCalculator.Plan(profile.Gender, profile.HeightCm, profile.WeightKg, calories.Value);
    }
}
