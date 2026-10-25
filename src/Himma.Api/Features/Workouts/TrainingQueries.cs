using Himma.Api.Data;
using Himma.Api.Domain;
using Himma.Api.Domain.Training;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Features.Workouts;

/// <summary>Reads completed sets for the progression hints and the weekly volume.</summary>
public sealed class TrainingQueries(AppDbContext db)
{
    public const int SessionsForHints = 2;

    /// <summary>
    /// The last <see cref="SessionsForHints"/> sessions before the given one, per exercise, newest first,
    /// each with its completed sets.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, IReadOnlyList<IReadOnlyList<PastSet>>>> RecentSessionsAsync(
        string userId, IReadOnlyCollection<int> exerciseIds, int sessionId, DateOnly date, DateTime startedAtUtc,
        CancellationToken cancellationToken)
    {
        if (exerciseIds.Count == 0)
        {
            return new Dictionary<int, IReadOnlyList<IReadOnlyList<PastSet>>>();
        }

        var ids = exerciseIds.ToList();
        var rows = await (
            from log in db.WorkoutSetLogs
            join session in db.WorkoutSessions on log.SessionId equals session.Id
            where session.UserId == userId && session.Id != sessionId && log.Completed && ids.Contains(log.ExerciseId)
                && (session.Date < date || (session.Date == date && session.StartedAtUtc < startedAtUtc))
            select new { log.ExerciseId, session.Id, session.Date, session.StartedAtUtc, log.Reps, log.WeightKg, log.Rir })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows.GroupBy(r => r.ExerciseId).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<IReadOnlyList<PastSet>>)g
                .GroupBy(r => new { r.Id, r.Date, r.StartedAtUtc })
                .OrderByDescending(s => s.Key.Date).ThenByDescending(s => s.Key.StartedAtUtc)
                .Take(SessionsForHints)
                .Select(s => (IReadOnlyList<PastSet>)s.Select(r => new PastSet(r.Reps, r.WeightKg, r.Rir)).ToList())
                .ToList());
    }

    public async Task<IReadOnlyList<WeekVolume>> WeeklyVolumeAsync(string userId, DateOnly today, int weeks, CancellationToken cancellationToken)
    {
        var firstDay = WeeklyVolume.WeekStart(today).AddDays(-7 * (weeks - 1));
        var sets = await (
            from log in db.WorkoutSetLogs
            join session in db.WorkoutSessions on log.SessionId equals session.Id
            join exercise in db.Exercises on log.ExerciseId equals exercise.Id
            where session.UserId == userId && log.Completed && session.Date >= firstDay && session.Date <= today
            select new { session.Date, exercise.MuscleGroup, log.Reps, log.WeightKg })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return WeeklyVolume.Build(sets.Select(s => (s.Date, s.MuscleGroup, s.Reps, s.WeightKg)), today, weeks);
    }
}

public sealed record VolumeResponse(int TargetMinSets, int TargetMaxSets, IReadOnlyList<WeekVolume> Weeks);
