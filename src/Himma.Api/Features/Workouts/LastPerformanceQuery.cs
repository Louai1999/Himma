using Himma.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Features.Workouts;

/// <summary>"Last time you did 60 kg x 8": the top completed set of the most recent session per exercise.</summary>
public sealed class LastPerformanceQuery(AppDbContext db)
{
    public async Task<IReadOnlyDictionary<int, LastPerformanceDto>> GetAsync(
        string userId,
        IReadOnlyCollection<int> exerciseIds,
        int? excludeSessionId,
        CancellationToken cancellationToken)
    {
        if (exerciseIds.Count == 0)
        {
            return new Dictionary<int, LastPerformanceDto>();
        }

        var ids = exerciseIds.ToList();
        var excluded = excludeSessionId ?? 0;
        var rows = await (
            from log in db.WorkoutSetLogs
            join session in db.WorkoutSessions on log.SessionId equals session.Id
            where session.UserId == userId
                && session.Id != excluded
                && log.Completed
                && ids.Contains(log.ExerciseId)
            select new { log.ExerciseId, session.Id, session.Date, session.StartedAtUtc, log.Reps, log.WeightKg })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.ExerciseId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var latest = g.MaxBy(r => (r.Date, r.StartedAtUtc))!;
                    var top = g.Where(r => r.Id == latest.Id)
                        .OrderByDescending(r => r.WeightKg ?? 0)
                        .ThenByDescending(r => r.Reps)
                        .First();
                    return new LastPerformanceDto(top.Date, top.WeightKg, top.Reps);
                });
    }
}
