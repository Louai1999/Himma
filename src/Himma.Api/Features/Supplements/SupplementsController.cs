using Himma.Api.Data;
using Himma.Api.Domain.Entities;
using Himma.Api.Domain.Nutrition;
using Himma.Api.Domain.Supplements;
using Himma.Api.Infrastructure.Http;
using Himma.Api.Infrastructure.Localization;
using Himma.Api.Infrastructure.RateLimiting;
using Himma.Api.Infrastructure.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Features.Supplements;

[Route("api/supplements")]
public sealed class SupplementsController(AppDbContext db, SupplementIntakeService intakes, TakeTokens takeTokens) : HimmaControllerBase
{
    public const int MaxSupplementsPerUser = 20;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SupplementDto>>> List(CancellationToken cancellationToken)
    {
        var userId = UserId;
        var supplements = await db.Supplements.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return supplements.Select(SupplementDto.From).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<SupplementDto>> Create(SupplementRequest request, CancellationToken cancellationToken)
    {
        var userId = UserId;
        if (await db.Supplements.CountAsync(s => s.UserId == userId, cancellationToken) >= MaxSupplementsPerUser)
        {
            return InvalidField("name", Lang.T($"يمكنك إضافة {MaxSupplementsPerUser} مكملاً على الأكثر.", $"You can add at most {MaxSupplementsPerUser} supplements."));
        }

        var supplement = new Supplement { UserId = userId, CreatedAtUtc = DateTime.UtcNow };
        Apply(supplement, request);
        db.Supplements.Add(supplement);
        await db.SaveChangesAsync(cancellationToken);
        return StatusCode(StatusCodes.Status201Created, SupplementDto.From(supplement));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<SupplementDto>> Update(int id, SupplementRequest request, CancellationToken cancellationToken)
    {
        var userId = UserId;
        var supplement = await db.Supplements.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, cancellationToken);
        if (supplement is null)
        {
            return NotFoundProblem();
        }

        Apply(supplement, request);
        await db.SaveChangesAsync(cancellationToken);
        return SupplementDto.From(supplement);
    }

    /// <summary>Deletes the supplement and its dose history. Meal entries it added stay, like any eaten meal.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = UserId;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SupplementIntakes.Where(i => i.SupplementId == id && i.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        var deleted = await db.Supplements.Where(s => s.Id == id && s.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return deleted == 0 ? NotFoundProblem() : NoContent();
    }

    /// <summary>Every supplement with what is due on that date, what was taken, and the streak.</summary>
    [HttpGet("day/{date}")]
    public async Task<ActionResult<SupplementDayResponse>> Day(string date, CancellationToken cancellationToken)
    {
        if (!ApiDate.TryParse(date, out var day))
        {
            return InvalidField("date", ApiDate.InvalidMessage);
        }

        var userId = UserId;
        var supplements = await db.Supplements.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var since = day.AddDays(-SupplementSchedule.StreakLookbackDays);
        var history = await db.SupplementIntakes.AsNoTracking()
            .Where(i => i.UserId == userId && i.Date >= since && i.Date <= day)
            .ToListAsync(cancellationToken);

        var items = supplements.Select(s =>
        {
            var mine = history.Where(i => i.SupplementId == s.Id).ToList();
            var takenByDate = mine.GroupBy(i => i.Date).ToDictionary(g => g.Key, g => g.Count());
            return new SupplementDayItem(
                SupplementDto.From(s),
                SupplementSchedule.IsScheduledOn(s, day),
                SupplementSchedule.DosesOn(s, day),
                mine.Where(i => i.Date == day).OrderBy(i => i.TakenAtUtc).Select(IntakeDto.From).ToList(),
                SupplementSchedule.Streak(s, takenByDate, day));
        }).ToList();

        return new SupplementDayResponse(day, items);
    }

    /// <summary>409 when the day's doses are already logged; send force=true to log another one anyway.</summary>
    [HttpPost("{id:int}/intakes")]
    public async Task<IActionResult> Take(int id, IntakeRequest request, CancellationToken cancellationToken)
    {
        var result = await intakes.TakeAsync(UserId, id, ApiDate.Parse(request.Date), request.Force, cancellationToken);
        return result.Outcome switch
        {
            IntakeOutcome.NotFound => NotFoundProblem(),
            IntakeOutcome.AlreadyTaken => AlreadyTakenProblem(result.Previous!),
            _ => StatusCode(StatusCodes.Status201Created, IntakeDto.From(result.Intake!)),
        };
    }

    [HttpDelete("intakes/{id:int}")]
    public async Task<IActionResult> Undo(int id, CancellationToken cancellationToken) =>
        await intakes.UndoAsync(UserId, id, cancellationToken) ? NoContent() : NotFoundProblem();

    /// <summary>
    /// "Taken" from a notification. No access token: the signed take token decides who and what (see TakeTokens).
    /// Never forces, so tapping twice or replaying the token cannot log extra doses.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("take")]
    public async Task<ActionResult<TakeFromNotificationResponse>> TakeFromNotification(TakeFromNotificationRequest request, CancellationToken cancellationToken)
    {
        var token = takeTokens.Read(request.Token);
        var activeUser = token is not null && await db.Users.AnyAsync(
            u => u.Id == token.UserId && u.IsActive && u.DeletedAtUtc == null, cancellationToken);
        if (token is null || !activeUser)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: Lang.T("انتهت صلاحية التذكير", "This reminder has expired"),
                detail: Lang.T("افتح التطبيق وسجّل الجرعة من هناك.", "Open the app and log the dose there."));
        }

        var result = await intakes.TakeAsync(token.UserId, token.SupplementId, token.Date, force: false, cancellationToken);
        return result.Outcome == IntakeOutcome.NotFound
            ? NotFoundProblem()
            : new TakeFromNotificationResponse(result.Outcome == IntakeOutcome.AlreadyTaken, result.Supplement!.Name);
    }

    private ObjectResult AlreadyTakenProblem(SupplementIntake previous)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext,
            statusCode: StatusCodes.Status409Conflict,
            title: Lang.T("سجّلت هذه الجرعة اليوم", "You already logged this dose today"),
            detail: Lang.T("جرعات اليوم مسجّلة. هل تريد تسجيل جرعة إضافية؟", "Today's doses are logged. Log an extra dose?"));
        problem.Extensions["takenAtUtc"] = previous.TakenAtUtc;
        return new ObjectResult(problem) { StatusCode = StatusCodes.Status409Conflict };
    }

    private static void Apply(Supplement supplement, SupplementRequest request)
    {
        supplement.Name = request.Name.Trim();
        // Two decimals: small doses such as 0.25 g must survive.
        supplement.Dose = Math.Round(request.Dose!.Value, 2, MidpointRounding.AwayFromZero);
        supplement.Unit = request.Unit!.Value;
        supplement.Times = SupplementSchedule.FormatTimes(
            request.Times.Select(t => TimeOnly.ParseExact(t, "HH:mm", System.Globalization.CultureInfo.InvariantCulture)));
        supplement.DaysMask = SupplementSchedule.ToMask(request.Days);
        supplement.ProteinG = request.ProteinG is { } protein ? NutritionMath.Round1(protein) : null;
        supplement.Calories = request.Calories is { } kcal ? NutritionMath.Round1(kcal) : null;
        supplement.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
    }
}
