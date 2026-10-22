using Himma.Api.Data;
using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Features.Workouts;

public sealed class PlanService(AppDbContext db, LastPerformanceQuery lastPerformance)
{
    /// <summary>The plan with days, items and exercises, or null if it does not exist or is not the user's.</summary>
    public async Task<PlanDetailDto?> GetDetailAsync(int planId, string userId, CancellationToken cancellationToken)
    {
        var plan = await db.WorkoutPlans.AsNoTracking()
            .Include(p => p.Days).ThenInclude(d => d.Items).ThenInclude(i => i.Exercise)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == planId && p.UserId == userId, cancellationToken);
        if (plan is null)
        {
            return null;
        }

        var exerciseIds = plan.Days.SelectMany(d => d.Items).Select(i => i.ExerciseId).Distinct().ToList();
        var last = await lastPerformance.GetAsync(userId, exerciseIds, excludeSessionId: null, cancellationToken);
        return PlanDetailDto.From(plan, last);
    }

    /// <summary>A new week: seven days, all rest until the user picks muscles for a day.</summary>
    public static List<WorkoutPlanDay> EmptyWeek() =>
        Enum.GetValues<DayKey>().Select(key => new WorkoutPlanDay { DayKey = key, IsRest = true }).ToList();

    public async Task<WorkoutPlan> CreateAsync(string userId, string name, WorkoutPlace place, string? templateKey, CancellationToken cancellationToken)
    {
        // The first plan a user creates becomes active automatically.
        var hasActive = await db.WorkoutPlans.AnyAsync(p => p.UserId == userId && p.IsActive, cancellationToken);
        var plan = new WorkoutPlan
        {
            UserId = userId,
            Name = name,
            Place = place,
            IsActive = !hasActive,
            BasedOnTemplateKey = templateKey,
            CreatedAtUtc = DateTime.UtcNow,
            Days = EmptyWeek(),
        };
        db.WorkoutPlans.Add(plan);
        return plan;
    }

    /// <summary>Copies a template into a new plan owned by the user and makes it the active plan.</summary>
    public async Task<WorkoutPlan?> CreateFromTemplateAsync(string templateKey, string userId, CancellationToken cancellationToken)
    {
        var template = WorkoutTemplates.Find(templateKey);
        if (template is null)
        {
            return null;
        }

        var exerciseIds = await SystemExerciseIdsByNameAsync(cancellationToken);

        await DeactivateAllAsync(userId, cancellationToken);
        var plan = await CreateAsync(userId, template.Name, template.Place, template.Key, cancellationToken);
        plan.IsActive = true;

        foreach (var templateDay in template.Days)
        {
            var day = plan.Days.Single(d => d.DayKey == templateDay.DayKey);
            day.IsRest = false;
            day.Focus = FocusCodec.Encode(templateDay.Focus);
            day.Items = templateDay.Items
                .Where(item => exerciseIds.ContainsKey(item.ExerciseName))
                .Select((item, index) => new WorkoutPlanItem
                {
                    ExerciseId = exerciseIds[item.ExerciseName],
                    Order = index,
                    Sets = item.Sets,
                    Reps = item.Reps,
                    RestSeconds = item.RestSeconds,
                })
                .ToList();
        }

        await db.SaveChangesAsync(cancellationToken);
        return plan;
    }

    public async Task<IReadOnlyList<TemplateDto>> GetTemplatesAsync(CancellationToken cancellationToken)
    {
        var exercises = await db.Exercises.AsNoTracking()
            .Where(e => e.OwnerUserId == null)
            .ToDictionaryAsync(e => e.Name, cancellationToken);

        return WorkoutTemplates.All.Select(t => new TemplateDto(
            t.Key,
            t.Name,
            t.Description,
            t.Place,
            Enum.GetValues<DayKey>().Select(key =>
            {
                var day = t.Days.FirstOrDefault(d => d.DayKey == key);
                return day is null
                    ? new TemplateDayDto(key, true, [], [])
                    : new TemplateDayDto(key, false, day.Focus, day.Items
                        .Where(i => exercises.ContainsKey(i.ExerciseName))
                        .Select(i => new TemplateItemDto(ExerciseDto.From(exercises[i.ExerciseName]), i.Sets, i.Reps, i.RestSeconds))
                        .ToList());
            }).ToList())).ToList();
    }

    public Task DeactivateAllAsync(string userId, CancellationToken cancellationToken) =>
        db.WorkoutPlans
            .Where(p => p.UserId == userId && p.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false), cancellationToken);

    private Task<Dictionary<string, int>> SystemExerciseIdsByNameAsync(CancellationToken cancellationToken) =>
        db.Exercises.AsNoTracking()
            .Where(e => e.OwnerUserId == null)
            .ToDictionaryAsync(e => e.Name, e => e.Id, cancellationToken);
}
