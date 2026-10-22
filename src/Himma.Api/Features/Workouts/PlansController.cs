using Himma.Api.Data;
using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Http;
using Himma.Api.Infrastructure.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Himma.Api.Infrastructure.Localization;

namespace Himma.Api.Features.Workouts;

/// <summary>The user's own workout plans. Every query is filtered by the signed-in user.</summary>
[Route("api/plans")]
public sealed class PlansController(AppDbContext db, PlanService plans) : HimmaControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlanSummaryDto>>> List(CancellationToken cancellationToken)
    {
        var userId = UserId;
        return await db.WorkoutPlans.AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.IsActive)
            .ThenByDescending(p => p.CreatedAtUtc)
            .Select(p => new PlanSummaryDto(p.Id, p.Name, p.Place, p.IsActive, p.BasedOnTemplateKey, p.CreatedAtUtc,
                p.Days.Count(d => !d.IsRest)))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Ready plans for beginners. Read-only; apply one with POST /api/plans/from-template/{key}.</summary>
    [HttpGet("templates")]
    public async Task<ActionResult<IReadOnlyList<TemplateDto>>> Templates(CancellationToken cancellationToken) =>
        Ok(await plans.GetTemplatesAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<PlanDetailDto>> Create(PlanRequest request, CancellationToken cancellationToken)
    {
        var plan = await plans.CreateAsync(UserId, request.Name.Trim(), request.Place!.Value, templateKey: null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return StatusCode(StatusCodes.Status201Created, await plans.GetDetailAsync(plan.Id, UserId, cancellationToken));
    }

    [HttpPost("from-template/{key}")]
    public async Task<ActionResult<PlanDetailDto>> CreateFromTemplate(string key, CancellationToken cancellationToken)
    {
        var plan = await plans.CreateFromTemplateAsync(key, UserId, cancellationToken);
        return plan is null
            ? NotFoundProblem(Lang.T("هذه الخطة الجاهزة غير موجودة.", "This ready plan does not exist."))
            : StatusCode(StatusCodes.Status201Created, await plans.GetDetailAsync(plan.Id, UserId, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PlanDetailDto>> Get(int id, CancellationToken cancellationToken)
    {
        var plan = await plans.GetDetailAsync(id, UserId, cancellationToken);
        return plan is null ? NotFoundProblem() : plan;
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PlanDetailDto>> Update(int id, PlanRequest request, CancellationToken cancellationToken)
    {
        var userId = UserId;
        var plan = await db.WorkoutPlans.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, cancellationToken);
        if (plan is null)
        {
            return NotFoundProblem();
        }

        plan.Name = request.Name.Trim();
        plan.Place = request.Place!.Value;
        await db.SaveChangesAsync(cancellationToken);
        return (await plans.GetDetailAsync(id, userId, cancellationToken))!;
    }

    /// <summary>Deletes the plan. Workout history stays; old sessions just lose their link to the plan day.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = UserId;
        var plan = await db.WorkoutPlans.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, cancellationToken);
        if (plan is null)
        {
            return NotFoundProblem();
        }

        db.WorkoutPlans.Remove(plan);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/activate")]
    public async Task<ActionResult<PlanDetailDto>> Activate(int id, CancellationToken cancellationToken)
    {
        var userId = UserId;
        var plan = await db.WorkoutPlans.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, cancellationToken);
        if (plan is null)
        {
            return NotFoundProblem();
        }

        await plans.DeactivateAllAsync(userId, cancellationToken);
        plan.IsActive = true;
        await db.SaveChangesAsync(cancellationToken);
        return (await plans.GetDetailAsync(id, userId, cancellationToken))!;
    }

    /// <summary>Replaces one day: rest flag, focus muscles and the ordered exercises with their settings.</summary>
    [HttpPut("{id:int}/days/{dayKey}")]
    public async Task<ActionResult<PlanDayDto>> UpdateDay(int id, string dayKey, PlanDayRequest request, CancellationToken cancellationToken)
    {
        if (!EnumParam.TryParse<DayKey>(dayKey, out var key))
        {
            return InvalidField("dayKey", Lang.T("اليوم يجب أن يكون من sat إلى fri.", "The day must be one of sat to fri."));
        }

        var userId = UserId;
        var day = await db.WorkoutPlanDays
            .Include(d => d.Items)
            .FirstOrDefaultAsync(d => d.PlanId == id && d.DayKey == key && d.Plan!.UserId == userId, cancellationToken);
        if (day is null)
        {
            return NotFoundProblem();
        }

        // Exercises must be system exercises or the user's own. Archived ones may stay if already on this day.
        var requestedIds = request.Items.Select(i => i.ExerciseId!.Value).Distinct().ToList();
        var allowed = await db.Exercises.AsNoTracking()
            .Where(e => requestedIds.Contains(e.Id) && (e.OwnerUserId == null || e.OwnerUserId == userId))
            .Select(e => new { e.Id, e.IsArchived })
            .ToDictionaryAsync(e => e.Id, e => e.IsArchived, cancellationToken);
        var alreadyOnDay = day.Items.Select(i => i.ExerciseId).ToHashSet();

        for (var i = 0; i < request.Items.Count; i++)
        {
            var exerciseId = request.Items[i].ExerciseId!.Value;
            if (!allowed.TryGetValue(exerciseId, out var isArchived) || (isArchived && !alreadyOnDay.Contains(exerciseId)))
            {
                return InvalidField($"items[{i}].exerciseId", Lang.T("هذا التمرين غير موجود في مكتبتك.", "This exercise is not in your library."));
            }
        }

        day.IsRest = request.IsRest!.Value;
        day.Focus = FocusCodec.Encode(request.Focus);
        db.WorkoutPlanItems.RemoveRange(day.Items);
        day.Items = request.Items.Select((item, index) => new WorkoutPlanItem
        {
            ExerciseId = item.ExerciseId!.Value,
            Order = index,
            Sets = item.Sets!.Value,
            Reps = item.Reps!.Value,
            WeightKg = item.WeightKg,
            RestSeconds = item.RestSeconds,
            Notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim(),
        }).ToList();

        await db.SaveChangesAsync(cancellationToken);

        var detail = await plans.GetDetailAsync(id, userId, cancellationToken);
        return detail!.Days.Single(d => d.DayKey == key);
    }
}
