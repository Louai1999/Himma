using Himma.Api.Data;
using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Http;
using Himma.Api.Infrastructure.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Features.Workouts;

/// <summary>
/// The exercise library: system exercises (read-only, can be cloned) plus the user's own.
/// A user never sees, edits or uses another user's exercises.
/// </summary>
[Route("api/exercises")]
public sealed class ExercisesController(AppDbContext db) : HimmaControllerBase
{
    private const string DefaultIconKey = "custom";

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExerciseDto>>> List(
        [FromQuery] string? place,
        [FromQuery] string? muscle,
        [FromQuery] string? region,
        [FromQuery] string? search,
        [FromQuery] bool mine = false,
        CancellationToken cancellationToken = default)
    {
        var userId = UserId;
        var query = db.Exercises.AsNoTracking()
            .Where(e => !e.IsArchived && (mine ? e.OwnerUserId == userId : e.OwnerUserId == null || e.OwnerUserId == userId));

        if (place is not null)
        {
            if (!EnumParam.TryParse<WorkoutPlace>(place, out var p)) return InvalidField("place", "مكان غير معروف.");
            query = query.Where(e => e.Place == p);
        }
        if (muscle is not null)
        {
            if (!EnumParam.TryParse<MuscleGroup>(muscle, out var m)) return InvalidField("muscle", "عضلة غير معروفة.");
            query = query.Where(e => e.MuscleGroup == m);
        }
        if (region is not null)
        {
            if (!EnumParam.TryParse<BodyRegion>(region, out var r)) return InvalidField("region", "منطقة غير معروفة.");
            query = query.Where(e => e.Region == r);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            if (term.Length > 60) return InvalidField("search", "نص البحث طويل جداً.");
            query = query.Where(e => e.Name.Contains(term));
        }

        var exercises = await query
            .OrderBy(e => e.OwnerUserId == null) // the user's own exercises first
            .ThenBy(e => e.MuscleGroup)
            .ThenBy(e => e.Name)
            .ToListAsync(cancellationToken);

        return exercises.Select(ExerciseDto.From).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<ExerciseDto>> Create(ExerciseRequest request, CancellationToken cancellationToken)
    {
        var exercise = new ExerciseDefinition { OwnerUserId = UserId, CreatedAtUtc = DateTime.UtcNow };
        Apply(exercise, request);
        db.Exercises.Add(exercise);
        await db.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ExerciseDto.From(exercise));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ExerciseDto>> Update(int id, ExerciseRequest request, CancellationToken cancellationToken)
    {
        var exercise = await FindVisibleAsync(id, cancellationToken);
        if (exercise is null || exercise.IsArchived)
        {
            return NotFoundProblem();
        }
        if (exercise.OwnerUserId is null)
        {
            return SystemExerciseIsReadOnly();
        }

        Apply(exercise, request);
        await db.SaveChangesAsync(cancellationToken);
        return ExerciseDto.From(exercise);
    }

    /// <summary>
    /// Deletes the user's own exercise. If a plan or the workout history uses it, it is archived instead,
    /// so those keep showing it.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var exercise = await FindVisibleAsync(id, cancellationToken);
        if (exercise is null || exercise.IsArchived)
        {
            return NotFoundProblem();
        }
        if (exercise.OwnerUserId is null)
        {
            return SystemExerciseIsReadOnly();
        }

        var isUsed = await db.WorkoutPlanItems.AnyAsync(i => i.ExerciseId == id, cancellationToken)
            || await db.WorkoutSetLogs.AnyAsync(l => l.ExerciseId == id, cancellationToken);

        if (isUsed)
        {
            exercise.IsArchived = true;
        }
        else
        {
            db.Exercises.Remove(exercise);
        }

        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Copies a system exercise (or one of the user's own) into a new exercise the user can edit.</summary>
    [HttpPost("{id:int}/clone")]
    public async Task<ActionResult<ExerciseDto>> Clone(int id, CancellationToken cancellationToken)
    {
        var source = await FindVisibleAsync(id, cancellationToken);
        if (source is null || source.IsArchived)
        {
            return NotFoundProblem();
        }

        var copy = new ExerciseDefinition
        {
            OwnerUserId = UserId,
            Name = source.Name,
            MuscleGroup = source.MuscleGroup,
            Region = source.Region,
            Place = source.Place,
            Equipment = source.Equipment,
            IconKey = source.IconKey,
            DefaultSets = source.DefaultSets,
            DefaultReps = source.DefaultReps,
            Instructions = source.Instructions,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Exercises.Add(copy);
        await db.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ExerciseDto.From(copy));
    }

    /// <summary>System exercises and the user's own. Another user's exercise is treated as not existing.</summary>
    private Task<ExerciseDefinition?> FindVisibleAsync(int id, CancellationToken cancellationToken)
    {
        var userId = UserId;
        return db.Exercises.FirstOrDefaultAsync(
            e => e.Id == id && (e.OwnerUserId == null || e.OwnerUserId == userId), cancellationToken);
    }

    private ObjectResult SystemExerciseIsReadOnly() =>
        Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "تمرين للقراءة فقط",
            detail: "تمارين النظام لا يمكن تعديلها أو حذفها. انسخ التمرين ثم عدّل نسختك.");

    private static void Apply(ExerciseDefinition exercise, ExerciseRequest request)
    {
        exercise.Name = request.Name.Trim();
        exercise.MuscleGroup = request.MuscleGroup!.Value;
        exercise.Region = request.Region!.Value;
        exercise.Place = request.Place!.Value;
        exercise.Equipment = request.Equipment?.Trim() ?? string.Empty;
        exercise.IconKey = request.IconKey ?? DefaultIconKey;
        exercise.DefaultSets = request.DefaultSets!.Value;
        exercise.DefaultReps = request.DefaultReps!.Value;
        exercise.Instructions = string.IsNullOrWhiteSpace(request.Instructions) ? null : request.Instructions.Trim();
    }
}
