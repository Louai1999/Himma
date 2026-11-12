using Himma.Api.Data;
using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Domain.Nutrition;
using Himma.Api.Features.Admin;
using Himma.Api.Features.History;
using Himma.Api.Features.Meals;
using Himma.Api.Features.Profile;
using Himma.Api.Infrastructure.Audit;
using Himma.Api.Infrastructure.Http;
using Himma.Api.Infrastructure.Localization;
using Himma.Api.Infrastructure.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Features.Coaching;

/// <summary>
/// The coach dashboard. Every endpoint needs an active coach package, and a coach only ever sees
/// clients who joined them with their code and have not left. Opening a client's data is audit-logged.
/// </summary>
[Route("api/coach")]
public sealed class CoachController(
    AppDbContext db,
    CoachingService coaching,
    AdminMetricsService metrics,
    HistoryService history,
    AuditLogger audit) : HimmaControllerBase
{
    private const int MessagesShown = 30;

    /// <summary>404 when the user has no coach package (the app uses it to decide whether to show the coach link).</summary>
    [HttpGet("me")]
    public async Task<ActionResult<CoachAccountDto>> Me(CancellationToken cancellationToken)
    {
        var account = await coaching.FindAccountAsync(UserId, cancellationToken);
        if (account is null)
        {
            return NotCoach();
        }
        return CoachAccountDto.From(account, await coaching.ActiveClientCountAsync(UserId, cancellationToken));
    }

    [HttpPut("me")]
    public async Task<ActionResult<CoachAccountDto>> UpdateMe(CoachProfileRequest request, CancellationToken cancellationToken)
    {
        var (account, error) = await RequireActiveAsync(cancellationToken);
        if (account is null) return error!;

        account.BusinessName = request.BusinessName.Trim();
        account.Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();
        account.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return CoachAccountDto.From(account, await coaching.ActiveClientCountAsync(UserId, cancellationToken));
    }

    /// <summary>A new code; the old one stops working at once (current clients stay).</summary>
    [HttpPost("me/invite-code")]
    public async Task<ActionResult<CoachAccountDto>> NewInviteCode(CancellationToken cancellationToken)
    {
        var (account, error) = await RequireActiveAsync(cancellationToken);
        if (account is null) return error!;

        account.InviteCode = await coaching.NewInviteCodeAsync(cancellationToken);
        account.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return CoachAccountDto.From(account, await coaching.ActiveClientCountAsync(UserId, cancellationToken));
    }

    /// <summary>Every active client with their numbers; clients who need attention come first.</summary>
    [HttpGet("clients")]
    public async Task<ActionResult<CoachClientsResponse>> Clients(CancellationToken cancellationToken)
    {
        var (account, error) = await RequireActiveAsync(cancellationToken);
        if (account is null) return error!;

        var links = await db.CoachClients.AsNoTracking()
            .Where(l => l.CoachUserId == UserId && l.Status == CoachLinkStatus.Active)
            .Join(db.Users, l => l.ClientUserId, u => u.Id, (l, u) => new { l.ClientUserId, l.JoinedAtUtc, u.UserName, u.DisplayName, u.IsActive, u.DeletedAtUtc })
            .Where(x => x.IsActive && x.DeletedAtUtc == null)
            .ToListAsync(cancellationToken);

        var ids = links.Select(l => l.ClientUserId).ToList();
        var today = ApiDate.UtcToday;
        var now = DateTime.UtcNow;
        var userMetrics = await metrics.ComputeAsync(ids, today, cancellationToken);
        var lastWeek = await metrics.ComputeAsync(ids, today, cancellationToken, CoachingService.AttentionWindowDays);
        var weightChanges = await coaching.WeightChangesAsync(ids, today, AdminMetricsService.WindowDays, cancellationToken);
        var lastLogged = await coaching.LastLoggedAsync(ids, cancellationToken);
        var lastMessages = await db.CoachNotes.AsNoTracking()
            .Where(n => n.CoachUserId == UserId && ids.Contains(n.ClientUserId))
            .GroupBy(n => n.ClientUserId)
            .Select(g => new { ClientUserId = g.Key, Last = g.Max(n => n.CreatedAtUtc) })
            .ToDictionaryAsync(x => x.ClientUserId, x => x.Last, cancellationToken);

        var rows = links.Select(l =>
            {
                var m = userMetrics[l.ClientUserId];
                return new CoachClientRow(
                    l.ClientUserId,
                    l.UserName ?? string.Empty,
                    l.DisplayName,
                    l.JoinedAtUtc,
                    m.Goal,
                    m.Streak,
                    m.ConsistencyPercent,
                    m.CalorieAdherencePercent,
                    m.ProteinAdherencePercent,
                    m.WaterAdherencePercent,
                    lastLogged[l.ClientUserId],
                    weightChanges[l.ClientUserId],
                    lastMessages.TryGetValue(l.ClientUserId, out var last) ? last : null,
                    CoachingService.Attention(lastWeek[l.ClientUserId], lastLogged[l.ClientUserId], l.JoinedAtUtc, now));
            })
            .OrderByDescending(r => r.Attention.Count)
            .ThenBy(r => r.LastLoggedAtUtc ?? DateTime.MinValue)
            .ToList();

        return new CoachClientsResponse(CoachAccountDto.From(account, ids.Count), rows);
    }

    [HttpGet("clients/{id}")]
    public async Task<ActionResult<CoachClientDetail>> Client(string id, CancellationToken cancellationToken)
    {
        var (link, client, error) = await FindClientAsync(id, cancellationToken);
        if (link is null) return error!;

        var today = ApiDate.UtcToday;
        var month = await history.GetSummaryAsync(client!.Id, today, AdminMetricsService.WindowDays, cancellationToken);
        var twoWeeks = await history.GetSummaryAsync(client.Id, today, 14, cancellationToken);
        var lastWeek = (await metrics.ComputeAsync([client.Id], today, cancellationToken, CoachingService.AttentionWindowDays))[client.Id];
        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == client.Id, cancellationToken);
        var messages = await db.CoachNotes.AsNoTracking()
            .Where(n => n.CoachUserId == UserId && n.ClientUserId == client.Id)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(MessagesShown)
            .ToListAsync(cancellationToken);

        await audit.WriteAsync(AuditActions.CoachViewClient, client.Id, new { username = client.UserName }, cancellationToken);

        return new CoachClientDetail(
            client.Id,
            client.UserName ?? string.Empty,
            client.DisplayName,
            link.JoinedAtUtc,
            link.PrivateNote,
            profile is null ? null : ProfileDto.From(profile),
            profile is null ? null : TargetCalculator.Calculate(profile),
            month.Streak,
            month.Adherence,
            month.Weights,
            month.Weights.Count >= 2 ? Math.Round(month.Weights[^1].WeightKg - month.Weights[0].WeightKg, 1) : null,
            twoWeeks.Daily,
            await metrics.WorkoutComplianceAsync(client.Id, today, cancellationToken),
            CoachingService.Attention(lastWeek, (await coaching.LastLoggedAsync([client.Id], cancellationToken))[client.Id], link.JoinedAtUtc, DateTime.UtcNow),
            messages.Select(CoachNoteDto.From).ToList());
    }

    [HttpGet("clients/{id}/meals")]
    public async Task<ActionResult<DayMealsResponse>> Meals(string id, [FromQuery] string? date, CancellationToken cancellationToken)
    {
        if (!ApiDate.TryParse(date, out var day))
        {
            return InvalidField("date", ApiDate.InvalidMessage);
        }

        var (link, client, error) = await FindClientAsync(id, cancellationToken);
        if (link is null) return error!;

        var meals = await db.MealEntries.AsNoTracking()
            .Where(m => m.UserId == client!.Id && m.Date == day)
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        await audit.WriteAsync(AuditActions.CoachViewMeals, client!.Id, new { username = client.UserName, date = day }, cancellationToken);

        return new DayMealsResponse(day, meals.Select(MealDto.From).ToList(), new MealTotals(
            NutritionMath.Round1(meals.Sum(m => m.Calories)),
            NutritionMath.Round1(meals.Sum(m => m.ProteinG)),
            NutritionMath.Round1(meals.Sum(m => m.CarbsG)),
            NutritionMath.Round1(meals.Sum(m => m.FatG))));
    }

    /// <summary>The coach's private note about the client. The client never sees it.</summary>
    [HttpPut("clients/{id}/note")]
    public async Task<IActionResult> PrivateNote(string id, CoachPrivateNoteRequest request, CancellationToken cancellationToken)
    {
        var (link, _, error) = await FindClientAsync(id, cancellationToken);
        if (link is null) return error!;

        link.PrivateNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("clients/{id}/messages")]
    public async Task<ActionResult<CoachNoteDto>> SendMessage(string id, CoachMessageRequest request, CancellationToken cancellationToken)
    {
        var (link, client, error) = await FindClientAsync(id, cancellationToken);
        if (link is null) return error!;

        var account = (await coaching.FindAccountAsync(UserId, cancellationToken))!;
        var note = new CoachNote
        {
            CoachUserId = UserId,
            ClientUserId = client!.Id,
            Text = request.Text.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.CoachNotes.Add(note);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(AuditActions.CoachMessage, client.Id, new { username = client.UserName }, cancellationToken);
        await coaching.NotifyClientAsync(client.Id, account.BusinessName, note.Text, cancellationToken);
        return CoachNoteDto.From(note);
    }

    /// <summary>Sends a copy of one of the coach's own plans to the client.</summary>
    [HttpPost("clients/{id}/plans")]
    public async Task<ActionResult<AssignPlanResponse>> AssignPlan(string id, AssignPlanRequest request, CancellationToken cancellationToken)
    {
        var (link, client, error) = await FindClientAsync(id, cancellationToken);
        if (link is null) return error!;

        var plan = await coaching.CopyPlanToClientAsync(request.PlanId, UserId, client!.Id, request.Activate, cancellationToken);
        if (plan is null)
        {
            return NotFoundProblem(Lang.T("الخطة غير موجودة في حسابك.", "This plan is not in your account."));
        }

        await audit.WriteAsync(AuditActions.CoachAssignPlan, client.Id, new { username = client.UserName, plan = plan.Name }, cancellationToken);
        var account = (await coaching.FindAccountAsync(UserId, cancellationToken))!;
        await coaching.NotifyClientAsync(client.Id, account.BusinessName,
            $"أرسل لك خطة تمرين جديدة: {plan.Name}", cancellationToken, $"Sent you a new workout plan: {plan.Name}");
        return new AssignPlanResponse(plan.Id, plan.Name, plan.IsActive);
    }

    /// <summary>Stops coaching this client. Their data stays theirs; the coach just loses access.</summary>
    [HttpDelete("clients/{id}")]
    public async Task<IActionResult> RemoveClient(string id, CancellationToken cancellationToken)
    {
        var (link, client, error) = await FindClientAsync(id, cancellationToken);
        if (link is null) return error!;

        link.Status = CoachLinkStatus.Ended;
        link.EndedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(AuditActions.CoachRemoveClient, client!.Id, new { username = client.UserName }, cancellationToken);
        return NoContent();
    }

    // ---------- Helpers ----------

    private ObjectResult NotCoach() => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: Lang.T("لا توجد باقة مدرب", "No coach package"),
        detail: Lang.T("هذا الحساب ليس عليه باقة المدربين. تواصل مع إدارة هِمّة لتفعيلها.", "This account has no coach package. Contact the Himma team to get one."));

    private async Task<(CoachAccount? Account, ActionResult? Error)> RequireActiveAsync(CancellationToken cancellationToken)
    {
        var account = await coaching.FindAccountAsync(UserId, cancellationToken);
        if (account is null)
        {
            return (null, NotCoach());
        }
        if (!CoachingService.IsActive(account))
        {
            return (null, Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: Lang.T("انتهت باقة المدرب", "Coach package expired"),
                detail: Lang.T("انتهت مدة باقتك. جدّدها لتعود إلى متدربيك؛ بياناتهم وروابطهم محفوظة.", "Your package has ended. Renew it to get back to your clients; their links are kept.")));
        }
        return (account, null);
    }

    private async Task<(CoachClient? Link, ApplicationUser? Client, ActionResult? Error)> FindClientAsync(string clientId, CancellationToken cancellationToken)
    {
        var (account, error) = await RequireActiveAsync(cancellationToken);
        if (account is null) return (null, null, error);

        var link = await coaching.FindActiveLinkAsync(UserId, clientId, cancellationToken);
        var client = link is null ? null : await db.Users.FirstOrDefaultAsync(
            u => u.Id == clientId && u.IsActive && u.DeletedAtUtc == null, cancellationToken);
        if (link is null || client is null)
        {
            return (null, null, NotFoundProblem(Lang.T("هذا المتدرب ليس ضمن متدربيك.", "This person is not one of your clients.")));
        }
        return (link, client, null);
    }
}
