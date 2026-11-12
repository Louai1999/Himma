using Himma.Api.Data;
using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Audit;
using Himma.Api.Infrastructure.Http;
using Himma.Api.Infrastructure.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Features.Coaching;

/// <summary>
/// The client's side of coaching. Sharing data with a coach starts only here, by the client entering the
/// coach's code, and the client can stop it at any time.
/// </summary>
[Route("api/my-coach")]
public sealed class MyCoachController(AppDbContext db, CoachingService coaching, AuditLogger audit) : HimmaControllerBase
{
    private const int MessagesShown = 50;

    [HttpGet]
    public async Task<ActionResult<MyCoachResponse>> Get(CancellationToken cancellationToken)
    {
        var link = await coaching.FindClientLinkAsync(UserId, cancellationToken);
        if (link is null)
        {
            return new MyCoachResponse(null, null, [], 0);
        }

        var coach = await PublicInfoAsync(link.CoachUserId, cancellationToken);
        var messages = await db.CoachNotes.AsNoTracking()
            .Where(n => n.ClientUserId == UserId && n.CoachUserId == link.CoachUserId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(MessagesShown)
            .ToListAsync(cancellationToken);

        return new MyCoachResponse(coach, link.JoinedAtUtc, messages.Select(CoachNoteDto.From).ToList(), messages.Count(n => n.ReadAtUtc is null));
    }

    /// <summary>Who the code belongs to, so the client sees the coach's name before agreeing to share.</summary>
    [HttpGet("preview")]
    public async Task<ActionResult<CoachPublicDto>> Preview([FromQuery] string? code, CancellationToken cancellationToken)
    {
        var (account, error) = await FindJoinableAsync(code, cancellationToken);
        if (account is null) return error!;
        return (await PublicInfoAsync(account.UserId, cancellationToken))!;
    }

    [HttpPost("join")]
    public async Task<ActionResult<MyCoachResponse>> Join(JoinCoachRequest request, CancellationToken cancellationToken)
    {
        var (account, error) = await FindJoinableAsync(request.Code, cancellationToken);
        if (account is null) return error!;

        if (account.UserId == UserId)
        {
            return Conflict(Lang.T("لا يمكنك الانضمام إلى نفسك كمدرب.", "You cannot join yourself as a coach."));
        }
        if (await coaching.FindClientLinkAsync(UserId, cancellationToken) is not null)
        {
            return Conflict(Lang.T("أنت مرتبط بمدرب الآن. اترك مدربك الحالي أولاً.", "You already have a coach. Leave your current coach first."));
        }
        if (await coaching.ActiveClientCountAsync(account.UserId, cancellationToken) >= account.MaxClients)
        {
            return Conflict(Lang.T("اكتمل عدد المتدربين عند هذا المدرب. تواصل معه.", "This coach has no free places right now. Please contact them."));
        }

        db.CoachClients.Add(new CoachClient
        {
            CoachUserId = account.UserId,
            ClientUserId = UserId,
            Status = CoachLinkStatus.Active,
            JoinedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(AuditActions.JoinCoach, account.UserId, new { coach = account.BusinessName }, cancellationToken);
        return await Get(cancellationToken);
    }

    /// <summary>Stops sharing with the coach at once. Plans the coach sent stay in the client's account.</summary>
    [HttpDelete]
    public async Task<IActionResult> Leave(CancellationToken cancellationToken)
    {
        var link = await coaching.FindClientLinkAsync(UserId, cancellationToken);
        if (link is null)
        {
            return NotFoundProblem(Lang.T("لست مرتبطاً بمدرب.", "You do not have a coach."));
        }

        link.Status = CoachLinkStatus.Ended;
        link.EndedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(AuditActions.LeaveCoach, link.CoachUserId, null, cancellationToken);
        return NoContent();
    }

    [HttpPost("messages/read")]
    public async Task<IActionResult> MarkRead(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await db.CoachNotes
            .Where(n => n.ClientUserId == UserId && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAtUtc, now), cancellationToken);
        return NoContent();
    }

    private ObjectResult Conflict(string detail) => Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: Lang.T("لا يمكن الانضمام", "Cannot join"),
        detail: detail);

    /// <summary>A wrong code and an expired package look the same, so codes cannot be probed.</summary>
    private async Task<(CoachAccount? Account, ActionResult? Error)> FindJoinableAsync(string? code, CancellationToken cancellationToken)
    {
        var normalized = CoachingService.NormalizeCode(code);
        var account = normalized.Length == 0 ? null : await db.CoachAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.InviteCode == normalized, cancellationToken);
        var coachUsable = account is not null && CoachingService.IsActive(account) && await db.Users
            .AnyAsync(u => u.Id == account.UserId && u.IsActive && u.DeletedAtUtc == null, cancellationToken);
        if (!coachUsable)
        {
            return (null, NotFoundProblem(Lang.T("الكود غير صحيح أو انتهت صلاحيته. تأكد منه مع مدربك.", "This code is wrong or no longer valid. Check it with your coach.")));
        }
        return (account, null);
    }

    private async Task<CoachPublicDto?> PublicInfoAsync(string coachUserId, CancellationToken cancellationToken) =>
        await db.CoachAccounts.AsNoTracking()
            .Where(a => a.UserId == coachUserId)
            .Join(db.Users, a => a.UserId, u => u.Id, (a, u) => new CoachPublicDto(a.BusinessName, a.Bio, u.DisplayName))
            .FirstOrDefaultAsync(cancellationToken);
}
