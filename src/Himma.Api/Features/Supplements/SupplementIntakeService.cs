using Himma.Api.Data;
using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Domain.Nutrition;
using Himma.Api.Domain.Supplements;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Features.Supplements;

public enum IntakeOutcome { Taken, AlreadyTaken, NotFound }

public sealed record IntakeResult(IntakeOutcome Outcome, Supplement? Supplement, SupplementIntake? Intake, SupplementIntake? Previous);

/// <summary>Logging and undoing doses, shared by the app and the "Taken" button on notifications.</summary>
public sealed class SupplementIntakeService(AppDbContext db)
{
    /// <summary>
    /// Logs one dose. When the day's doses are already logged (at least one on days off the schedule),
    /// nothing is saved unless <paramref name="force"/> is set, so a double tap never doubles a dose.
    /// A supplement with protein or calories also adds a meal entry, linked so undo removes both.
    /// </summary>
    public async Task<IntakeResult> TakeAsync(string userId, int supplementId, DateOnly date, bool force, CancellationToken cancellationToken)
    {
        var supplement = await db.Supplements.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == supplementId && s.UserId == userId, cancellationToken);
        if (supplement is null)
        {
            return new IntakeResult(IntakeOutcome.NotFound, null, null, null);
        }

        var takenToday = await db.SupplementIntakes.AsNoTracking()
            .Where(i => i.SupplementId == supplementId && i.Date == date)
            .OrderBy(i => i.TakenAtUtc)
            .ToListAsync(cancellationToken);
        var limit = Math.Max(1, SupplementSchedule.DosesOn(supplement, date));
        if (takenToday.Count >= limit && !force)
        {
            return new IntakeResult(IntakeOutcome.AlreadyTaken, supplement, null, takenToday[^1]);
        }

        // The dose and its meal entry are saved together or not at all.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var intake = new SupplementIntake { UserId = userId, SupplementId = supplementId, Date = date, TakenAtUtc = now };
        if (supplement.ProteinG is > 0 || supplement.Calories is > 0)
        {
            var meal = new MealEntry
            {
                UserId = userId,
                Date = date,
                MealType = MealType.Snack,
                Source = MealSource.Supplement,
                Name = supplement.Name,
                Grams = supplement.Unit == SupplementUnit.Gram ? NutritionMath.Round1(supplement.Dose) : 0,
                Calories = NutritionMath.Round1(supplement.Calories ?? 0),
                ProteinG = NutritionMath.Round1(supplement.ProteinG ?? 0),
                CreatedAtUtc = now,
            };
            db.MealEntries.Add(meal);
            await db.SaveChangesAsync(cancellationToken);
            intake.MealEntryId = meal.Id;
        }

        db.SupplementIntakes.Add(intake);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new IntakeResult(IntakeOutcome.Taken, supplement, intake, takenToday.LastOrDefault());
    }

    /// <summary>Removes a dose and the meal entry it added. False when it does not exist for this user.</summary>
    public async Task<bool> UndoAsync(string userId, int intakeId, CancellationToken cancellationToken)
    {
        var intake = await db.SupplementIntakes.FirstOrDefaultAsync(i => i.Id == intakeId && i.UserId == userId, cancellationToken);
        if (intake is null)
        {
            return false;
        }

        if (intake.MealEntryId is { } mealId)
        {
            await db.MealEntries.Where(m => m.Id == mealId && m.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        }
        db.SupplementIntakes.Remove(intake);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
