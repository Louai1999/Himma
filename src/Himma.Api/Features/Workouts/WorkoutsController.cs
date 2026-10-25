using Himma.Api.Data;
using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Domain.Training;
using Himma.Api.Infrastructure.Http;
using Himma.Api.Infrastructure.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Himma.Api.Infrastructure.Localization;

namespace Himma.Api.Features.Workouts;

/// <summary>Workout sessions: start from a plan day, tick sets, complete, browse history.</summary>
[Route("api/workouts")]
public sealed class WorkoutsController(AppDbContext db, LastPerformanceQuery lastPerformance, TrainingQueries training) : HimmaControllerBase
{
    /// <summary>
    /// Starts a session. If an unfinished session for the same date and plan day already exists,
    /// that one is returned instead (opening the workout twice does not duplicate it).
    /// </summary>
    [HttpPost("sessions")]
    public async Task<ActionResult<SessionDto>> Start(StartSessionRequest request, CancellationToken cancellationToken)
    {
        var userId = UserId;
        var date = ApiDate.Parse(request.Date);

        if (request.PlanDayId is int planDayId
            && !await db.WorkoutPlanDays.AnyAsync(d => d.Id == planDayId && d.Plan!.UserId == userId, cancellationToken))
        {
            return InvalidField("planDayId", Lang.T("يوم الخطة غير موجود.", "This plan day does not exist."));
        }

        var existing = await db.WorkoutSessions.AsNoTracking().FirstOrDefaultAsync(s =>
            s.UserId == userId && s.Date == date && s.PlanDayId == request.PlanDayId && s.CompletedAtUtc == null,
            cancellationToken);
        if (existing is not null)
        {
            return await BuildAsync(existing, cancellationToken);
        }

        var session = new WorkoutSession
        {
            UserId = userId,
            Date = date,
            PlanDayId = request.PlanDayId,
            StartedAtUtc = DateTime.UtcNow,
        };
        db.WorkoutSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, await BuildAsync(session, cancellationToken));
    }

    [HttpGet("sessions/{id:int}")]
    public async Task<ActionResult<SessionDto>> Get(int id, CancellationToken cancellationToken)
    {
        var session = await FindOwnedAsync(id, tracking: false, cancellationToken);
        return session is null ? NotFoundProblem() : await BuildAsync(session, cancellationToken);
    }

    /// <summary>Saves the sets done so far (replaces the previous list). Weights are remembered for next time.</summary>
    [HttpPut("sessions/{id:int}/sets")]
    public async Task<ActionResult<SessionDto>> SaveSets(int id, SaveSetsRequest request, CancellationToken cancellationToken)
    {
        var session = await FindOwnedAsync(id, tracking: true, cancellationToken);
        if (session is null)
        {
            return NotFoundProblem();
        }
        if (session.CompletedAtUtc is not null)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: Lang.T("التمرين منتهٍ", "Workout finished"),
                detail: Lang.T("لا يمكن تعديل جولات تمرين تم إنهاؤه.", "Sets of a finished workout cannot be changed."));
        }

        var userId = UserId;
        var requestedIds = request.Sets.Select(s => s.ExerciseId!.Value).Distinct().ToList();
        var allowedIds = await db.Exercises.AsNoTracking()
            .Where(e => requestedIds.Contains(e.Id) && (e.OwnerUserId == null || e.OwnerUserId == userId))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
        var unknown = request.Sets.FindIndex(s => !allowedIds.Contains(s.ExerciseId!.Value));
        if (unknown >= 0)
        {
            return InvalidField($"sets[{unknown}].exerciseId", Lang.T("هذا التمرين غير موجود في مكتبتك.", "This exercise is not in your library."));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.WorkoutSetLogs.Where(l => l.SessionId == id).ExecuteDeleteAsync(cancellationToken);
        db.WorkoutSetLogs.AddRange(request.Sets.Select(s => new WorkoutSetLog
        {
            SessionId = id,
            ExerciseId = s.ExerciseId!.Value,
            SetNumber = s.SetNumber!.Value,
            Reps = s.Reps!.Value,
            WeightKg = s.WeightKg,
            Completed = s.Completed!.Value,
            Rir = s.Rir,
        }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await BuildAsync(session, cancellationToken);
    }

    [HttpPost("sessions/{id:int}/complete")]
    public async Task<ActionResult<SessionDto>> Complete(int id, CancellationToken cancellationToken)
    {
        var session = await FindOwnedAsync(id, tracking: true, cancellationToken);
        if (session is null)
        {
            return NotFoundProblem();
        }

        session.CompletedAtUtc ??= DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await BuildAsync(session, cancellationToken);
    }

    /// <summary>Sessions of the last <paramref name="days"/> days (ending at the user's local <paramref name="today"/>), newest first.</summary>
    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<WorkoutHistoryItemDto>>> History(
        [FromQuery] int days = 30,
        [FromQuery] string? today = null,
        CancellationToken cancellationToken = default)
    {
        if (days is < 1 or > 365)
        {
            return InvalidField("days", Lang.T("عدد الأيام بين 1 و 365.", "Days must be between 1 and 365."));
        }

        var todayDate = ApiDate.UtcToday;
        if (today is not null && (!ApiDate.TryParse(today, out todayDate) || !ApiDate.IsPlausibleToday(todayDate)))
        {
            return InvalidField("today", Lang.T("تاريخ اليوم غير صحيح.", "Today's date is not valid."));
        }

        var userId = UserId;
        var fromDate = todayDate.AddDays(-(days - 1));
        var sessions = await (
            from s in db.WorkoutSessions.AsNoTracking()
            where s.UserId == userId && s.Date >= fromDate && s.Date <= todayDate
            join d in db.WorkoutPlanDays on s.PlanDayId equals d.Id into dayJoin
            from d in dayJoin.DefaultIfEmpty()
            orderby s.Date descending, s.StartedAtUtc descending
            select new
            {
                s.Id,
                s.Date,
                DayKey = d == null ? (Domain.DayKey?)null : d.DayKey,
                PlanName = d == null ? null : d.Plan!.Name,
                s.StartedAtUtc,
                s.CompletedAtUtc,
                ExerciseCount = s.SetLogs.Select(l => l.ExerciseId).Distinct().Count(),
                CompletedSets = s.SetLogs.Count(l => l.Completed),
                Volume = s.SetLogs.Where(l => l.Completed).Sum(l => (l.WeightKg ?? 0) * l.Reps),
            })
            .ToListAsync(cancellationToken);

        return sessions.Select(s => new WorkoutHistoryItemDto(
            s.Id, s.Date, s.DayKey, s.PlanName, s.StartedAtUtc, s.CompletedAtUtc,
            s.ExerciseCount, s.CompletedSets, Math.Round(s.Volume, 1))).ToList();
    }

    /// <summary>Completed sets per muscle for the last <paramref name="weeks"/> weeks (Saturday to Friday).</summary>
    [HttpGet("volume")]
    public async Task<ActionResult<VolumeResponse>> Volume(
        [FromQuery] int weeks = 4,
        [FromQuery] string? today = null,
        CancellationToken cancellationToken = default)
    {
        if (weeks is < 1 or > 12)
        {
            return InvalidField("weeks", Lang.T("عدد الأسابيع بين 1 و 12.", "Weeks must be between 1 and 12."));
        }

        var todayDate = ApiDate.UtcToday;
        if (today is not null && (!ApiDate.TryParse(today, out todayDate) || !ApiDate.IsPlausibleToday(todayDate)))
        {
            return InvalidField("today", Lang.T("تاريخ اليوم غير صحيح.", "Today's date is not valid."));
        }

        var result = await training.WeeklyVolumeAsync(UserId, todayDate, weeks, cancellationToken);
        return new VolumeResponse(WeeklyVolume.TargetMinSets, WeeklyVolume.TargetMaxSets, result);
    }

    private Task<WorkoutSession?> FindOwnedAsync(int id, bool tracking, CancellationToken cancellationToken)
    {
        var userId = UserId;
        var query = tracking ? db.WorkoutSessions : db.WorkoutSessions.AsNoTracking();
        return query.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, cancellationToken);
    }

    private async Task<SessionDto> BuildAsync(WorkoutSession session, CancellationToken cancellationToken)
    {
        var day = session.PlanDayId is null
            ? null
            : await db.WorkoutPlanDays.AsNoTracking()
                .Include(d => d.Plan)
                .Include(d => d.Items).ThenInclude(i => i.Exercise)
                .FirstOrDefaultAsync(d => d.Id == session.PlanDayId, cancellationToken);

        var sets = await db.WorkoutSetLogs.AsNoTracking()
            .Where(l => l.SessionId == session.Id)
            .OrderBy(l => l.ExerciseId).ThenBy(l => l.SetNumber)
            .ToListAsync(cancellationToken);

        var planned = day?.Items.OrderBy(i => i.Order).ToList() ?? [];
        var exerciseIds = planned.Select(i => i.ExerciseId).Concat(sets.Select(s => s.ExerciseId)).Distinct().ToList();
        var last = await lastPerformance.GetAsync(session.UserId, exerciseIds, session.Id, cancellationToken);
        var recent = await training.RecentSessionsAsync(
            session.UserId, exerciseIds, session.Id, session.Date, session.StartedAtUtc, cancellationToken);
        ProgressionHint? Hint(int exerciseId, MuscleGroup muscle, int sets, int reps) =>
            recent.TryGetValue(exerciseId, out var history) ? Progression.Advise(muscle, sets, reps, history) : null;

        var exercises = planned.Select(i => new SessionExerciseDto(
            ExerciseDto.From(i.Exercise!), i.Sets, i.Reps, i.WeightKg, i.RestSeconds, i.Notes,
            last.GetValueOrDefault(i.ExerciseId),
            Hint(i.ExerciseId, i.Exercise!.MuscleGroup, i.Sets, i.Reps))).ToList();

        // Exercises logged outside the plan (free workouts, or one added during the session) are listed too.
        var plannedIds = planned.Select(i => i.ExerciseId).ToHashSet();
        var extraIds = sets.Select(s => s.ExerciseId).Where(id => !plannedIds.Contains(id)).Distinct().ToList();
        if (extraIds.Count > 0)
        {
            var extras = await db.Exercises.AsNoTracking()
                .Where(e => extraIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, cancellationToken);
            exercises.AddRange(extraIds.Where(extras.ContainsKey).Select(id =>
            {
                var logged = sets.Where(s => s.ExerciseId == id).ToList();
                var exercise = extras[id];
                return new SessionExerciseDto(
                    ExerciseDto.From(exercise), logged.Count, logged.Max(s => s.Reps), null, null, null,
                    last.GetValueOrDefault(id),
                    Hint(id, exercise.MuscleGroup, exercise.DefaultSets, exercise.DefaultReps));
            }));
        }

        return new SessionDto(
            session.Id,
            session.Date,
            session.PlanDayId,
            day?.DayKey,
            day?.Plan?.Name,
            session.StartedAtUtc,
            session.CompletedAtUtc,
            exercises,
            sets.Select(SetLogDto.From).ToList());
    }
}
