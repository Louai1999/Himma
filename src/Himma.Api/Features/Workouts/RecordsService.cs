using Himma.Api.Data;
using Himma.Api.Domain;
using Himma.Api.Domain.Strength;
using Himma.Api.Infrastructure.Localization;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Features.Workouts;

/// <summary>
/// Best results per exercise: the best estimated 1RM (from any weight × reps) and the heaviest weight lifted.
/// Only completed sets with a weight count.
/// </summary>
public sealed record PersonalRecordDto(
    int ExerciseId,
    string ExerciseName,
    MuscleGroup MuscleGroup,
    double BestOneRepMaxKg,
    double BestSetWeightKg,
    int BestSetReps,
    DateOnly BestSetDate,
    double HeaviestWeightKg,
    int HeaviestReps,
    DateOnly HeaviestDate,
    int Sessions);

public static class RecordKinds
{
    public const string OneRepMax = "oneRepMax";
    public const string Weight = "weight";
}

/// <summary>A record beaten in a session. PreviousKg is the best before it.</summary>
public sealed record NewRecordDto(int ExerciseId, string ExerciseName, string Kind, double ValueKg, double PreviousKg, double WeightKg, int Reps);

public sealed class RecordsService(AppDbContext db)
{
    private sealed record SetRow(int ExerciseId, int SessionId, DateOnly Date, DateTime StartedAtUtc, int Reps, double WeightKg)
    {
        public double OneRepMax => Domain.Strength.OneRepMax.Estimate(WeightKg, Reps);
    }

    public async Task<IReadOnlyList<PersonalRecordDto>> AllAsync(string userId, CancellationToken cancellationToken)
    {
        var rows = await RowsAsync(userId, exerciseIds: null, cancellationToken);
        var names = await NamesAsync(rows.Select(r => r.ExerciseId).Distinct().ToList(), cancellationToken);

        return rows.GroupBy(r => r.ExerciseId)
            .Select(g =>
            {
                var best = g.MaxBy(r => (r.OneRepMax, r.WeightKg))!;
                var heaviest = g.MaxBy(r => (r.WeightKg, r.Reps))!;
                var (name, muscle) = names[g.Key];
                return new PersonalRecordDto(g.Key, name, muscle,
                    best.OneRepMax, best.WeightKg, best.Reps, best.Date,
                    heaviest.WeightKg, heaviest.Reps, heaviest.Date,
                    g.Select(r => r.SessionId).Distinct().Count());
            })
            .OrderByDescending(r => r.BestSetDate > r.HeaviestDate ? r.BestSetDate : r.HeaviestDate)
            .ThenBy(r => r.ExerciseName)
            .ToList();
    }

    /// <summary>
    /// Records this session beat, compared with every earlier session. The first time an exercise is done
    /// is not a "record": there is nothing to beat yet. Null when the session is not the user's.
    /// </summary>
    public async Task<IReadOnlyList<NewRecordDto>?> NewInSessionAsync(string userId, int sessionId, CancellationToken cancellationToken)
    {
        var session = await db.WorkoutSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var exerciseIds = await db.WorkoutSetLogs.AsNoTracking()
            .Where(l => l.SessionId == sessionId && l.Completed)
            .Select(l => l.ExerciseId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var rows = await RowsAsync(userId, exerciseIds, cancellationToken);
        var names = await NamesAsync(exerciseIds, cancellationToken);

        var records = new List<NewRecordDto>();
        foreach (var exerciseId in exerciseIds)
        {
            var mine = rows.Where(r => r.ExerciseId == exerciseId && r.SessionId == sessionId).ToList();
            var before = rows.Where(r => r.ExerciseId == exerciseId && r.SessionId != sessionId
                && (r.Date, r.StartedAtUtc).CompareTo((session.Date, session.StartedAtUtc)) < 0).ToList();
            if (mine.Count == 0 || before.Count == 0)
            {
                continue;
            }

            var name = names[exerciseId].Name;
            var best = mine.MaxBy(r => r.OneRepMax)!;
            var previousBest = before.Max(r => r.OneRepMax);
            if (best.OneRepMax > previousBest)
            {
                records.Add(new NewRecordDto(exerciseId, name, RecordKinds.OneRepMax, best.OneRepMax, previousBest, best.WeightKg, best.Reps));
            }

            var heaviest = mine.MaxBy(r => (r.WeightKg, r.Reps))!;
            var previousHeaviest = before.Max(r => r.WeightKg);
            if (heaviest.WeightKg > previousHeaviest)
            {
                records.Add(new NewRecordDto(exerciseId, name, RecordKinds.Weight, heaviest.WeightKg, previousHeaviest, heaviest.WeightKg, heaviest.Reps));
            }
        }
        return records;
    }

    private async Task<List<SetRow>> RowsAsync(string userId, List<int>? exerciseIds, CancellationToken cancellationToken)
    {
        var query =
            from log in db.WorkoutSetLogs
            join session in db.WorkoutSessions on log.SessionId equals session.Id
            where session.UserId == userId && log.Completed && log.WeightKg > 0 && log.Reps >= 1
            select new { log.ExerciseId, log.SessionId, session.Date, session.StartedAtUtc, log.Reps, log.WeightKg };
        if (exerciseIds is not null)
        {
            query = query.Where(r => exerciseIds.Contains(r.ExerciseId));
        }

        return (await query.AsNoTracking().ToListAsync(cancellationToken))
            .Select(r => new SetRow(r.ExerciseId, r.SessionId, r.Date, r.StartedAtUtc, Math.Min(r.Reps, OneRepMax.MaxReps), r.WeightKg!.Value))
            .ToList();
    }

    private async Task<Dictionary<int, (string Name, MuscleGroup Muscle)>> NamesAsync(List<int> ids, CancellationToken cancellationToken) =>
        (await db.Exercises.AsNoTracking().Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, e.Name, e.NameEn, e.MuscleGroup })
            .ToListAsync(cancellationToken))
        .ToDictionary(e => e.Id, e => (Lang.Pick(e.Name, e.NameEn), e.MuscleGroup));
}
