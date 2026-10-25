using Himma.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Features.Account;

/// <summary>
/// Everything stored about one user, as plain JSON (for "export my data" and data-access requests).
/// Never includes password hashes, security stamps or tokens.
/// </summary>
public sealed class UserDataExporter(AppDbContext db)
{
    public async Task<object?> ExportAsync(string userId, CancellationToken cancellationToken)
    {
        var account = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.UserName,
                u.DisplayName,
                u.CreatedAtUtc,
                u.LastLoginAtUtc,
                u.PrivacyConsentAtUtc,
                u.IsActive,
                u.DeletedAtUtc,
                u.DeletionScheduledForUtc,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (account is null)
        {
            return null;
        }

        var plans = await db.WorkoutPlans.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new
            {
                p.Name,
                p.Place,
                p.IsActive,
                p.CreatedAtUtc,
                p.BasedOnTemplateKey,
                Days = p.Days.OrderBy(d => d.DayKey).Select(d => new
                {
                    d.DayKey,
                    d.IsRest,
                    d.Focus,
                    Items = d.Items.OrderBy(i => i.Order).Select(i => new
                    {
                        Exercise = i.Exercise!.Name,
                        i.Sets,
                        i.Reps,
                        i.WeightKg,
                        i.RestSeconds,
                        i.Notes,
                    }),
                }),
            })
            .ToListAsync(cancellationToken);

        var sessions = await db.WorkoutSessions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.Date)
            .Select(s => new
            {
                s.Date,
                s.StartedAtUtc,
                s.CompletedAtUtc,
                Sets = s.SetLogs.OrderBy(l => l.ExerciseId).ThenBy(l => l.SetNumber)
                    .Select(l => new { l.ExerciseId, l.SetNumber, l.Reps, l.WeightKg, l.Completed, l.Rir }),
            })
            .ToListAsync(cancellationToken);

        return new
        {
            ExportedAtUtc = DateTime.UtcNow,
            Account = account,
            Profile = await db.UserProfiles.AsNoTracking().Where(p => p.UserId == userId)
                .Select(p => new { p.Gender, p.Age, p.HeightCm, p.WeightKg, p.ActivityLevel, p.Goal, p.TargetWeightKg, p.TargetBodyFatPercent, p.UpdatedAtUtc })
                .FirstOrDefaultAsync(cancellationToken),
            Meals = await db.MealEntries.AsNoTracking().Where(m => m.UserId == userId).OrderBy(m => m.Date).ThenBy(m => m.CreatedAtUtc)
                .Select(m => new { m.Date, m.MealType, m.Name, m.Grams, m.Calories, m.ProteinG, m.CarbsG, m.FatG, m.Source, m.CreatedAtUtc })
                .ToListAsync(cancellationToken),
            Water = await db.WaterEntries.AsNoTracking().Where(w => w.UserId == userId).OrderBy(w => w.Date).ThenBy(w => w.CreatedAtUtc)
                .Select(w => new { w.Date, w.Ml, w.CreatedAtUtc })
                .ToListAsync(cancellationToken),
            Weights = await db.WeightEntries.AsNoTracking().Where(w => w.UserId == userId).OrderBy(w => w.Date)
                .Select(w => new { w.Date, w.WeightKg })
                .ToListAsync(cancellationToken),
            Supplements = await db.Supplements.AsNoTracking().Where(s => s.UserId == userId).OrderBy(s => s.CreatedAtUtc)
                .Select(s => new { s.Id, s.Name, s.Dose, s.Unit, s.Times, s.DaysMask, s.ProteinG, s.Calories, s.Notes, s.CreatedAtUtc })
                .ToListAsync(cancellationToken),
            SupplementIntakes = await db.SupplementIntakes.AsNoTracking().Where(i => i.UserId == userId).OrderBy(i => i.TakenAtUtc)
                .Select(i => new { i.SupplementId, i.Date, i.TakenAtUtc })
                .ToListAsync(cancellationToken),
            // Endpoints are left out on purpose: they work like a password for sending to the device.
            NotificationDevices = await db.PushDevices.AsNoTracking().Where(d => d.UserId == userId)
                .Select(d => new { d.TimeZone, d.Language, d.CreatedAtUtc })
                .ToListAsync(cancellationToken),
            Steps = await db.StepEntries.AsNoTracking().Where(s => s.UserId == userId).OrderBy(s => s.Date)
                .Select(s => new { s.Date, s.Steps })
                .ToListAsync(cancellationToken),
            BodyMeasurements = await db.BodyMeasurements.AsNoTracking().Where(m => m.UserId == userId).OrderBy(m => m.Date)
                .Select(m => new { m.Date, m.NeckCm, m.WaistCm, m.HipCm, m.ChestCm, m.ArmCm, m.ThighCm, m.BodyFatPercent, m.MuscleMassKg })
                .ToListAsync(cancellationToken),
            WaterReminder = await db.WaterReminderSettings.AsNoTracking().Where(r => r.UserId == userId)
                .Select(r => new { r.Enabled, r.IntervalHours })
                .FirstOrDefaultAsync(cancellationToken),
            CustomFoods = await db.CustomFoods.AsNoTracking().Where(f => f.UserId == userId)
                .Select(f => new { f.Name, f.KcalPer100g, f.ProteinPer100g, f.CreatedAtUtc })
                .ToListAsync(cancellationToken),
            Exercises = await db.Exercises.AsNoTracking().Where(e => e.OwnerUserId == userId)
                .Select(e => new { e.Id, e.Name, e.MuscleGroup, e.Region, e.Place, e.Equipment, e.DefaultSets, e.DefaultReps, e.Instructions, e.IsArchived })
                .ToListAsync(cancellationToken),
            Plans = plans,
            Workouts = sessions,
            CoachMessages = await db.CoachMessages.AsNoTracking().Where(c => c.UserId == userId).OrderBy(c => c.CreatedAtUtc)
                .Select(c => new { c.Role, c.Content, c.CreatedAtUtc })
                .ToListAsync(cancellationToken),
        };
    }
}
