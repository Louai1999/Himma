using Himma.Api.Data;
using Himma.Api.Domain.Entities;
using Himma.Api.Domain.Nutrition;
using Himma.Api.Features.Account;
using Himma.Api.Features.History;
using Himma.Api.Features.Meals;
using Himma.Api.Features.Profile;
using Himma.Api.Infrastructure.Audit;
using Himma.Api.Infrastructure.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Features.Admin;

/// <summary>Read-only views of subscribers. Opening one subscriber's data is written to the audit log.</summary>
[Route("api/admin")]
public sealed class AdminUsersController(
    UserManager<ApplicationUser> users,
    AppDbContext db,
    AdminMetricsService metrics,
    HistoryService history,
    UserDataExporter exporter,
    AuditLogger audit) : AdminControllerBase(users)
{
    [HttpGet("overview")]
    public async Task<ActionResult<AdminOverview>> Overview(CancellationToken cancellationToken) =>
        await metrics.OverviewAsync(ApiDate.UtcToday, cancellationToken);

    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<AdminUserRow>>> List([FromQuery] AdminUserQuery query, CancellationToken cancellationToken)
    {
        var today = ApiDate.UtcToday;
        var sort = query.Sort ?? AdminUserSort.JoinedDesc;
        var filtered = metrics.Subscribers().AsNoTracking();

        filtered = (query.Status ?? AdminUserStatusFilter.Current) switch
        {
            AdminUserStatusFilter.Active => filtered.Where(u => u.IsActive && u.DeletedAtUtc == null),
            AdminUserStatusFilter.Suspended => filtered.Where(u => !u.IsActive && u.DeletedAtUtc == null),
            AdminUserStatusFilter.PendingDeletion => filtered.Where(u => u.DeletedAtUtc != null),
            AdminUserStatusFilter.All => filtered,
            _ => filtered.Where(u => u.DeletedAtUtc == null),
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            filtered = filtered.Where(u => u.UserName!.ToLower().Contains(term) || u.DisplayName.ToLower().Contains(term));
        }
        if (!string.IsNullOrEmpty(query.JoinedFrom))
        {
            var from = ApiDate.Parse(query.JoinedFrom).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            filtered = filtered.Where(u => u.CreatedAtUtc >= from);
        }
        if (!string.IsNullOrEmpty(query.JoinedTo))
        {
            var toExclusive = ApiDate.Parse(query.JoinedTo).AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            filtered = filtered.Where(u => u.CreatedAtUtc < toExclusive);
        }

        var total = await filtered.CountAsync(cancellationToken);
        List<ApplicationUser> page;
        Dictionary<string, UserMetrics> pageMetrics;

        if (AdminUserSort.IsMetric(sort))
        {
            // Metric sorts need the numbers for every match before paging.
            var all = await filtered.ToListAsync(cancellationToken);
            var allMetrics = await metrics.ComputeAsync(all.Select(u => u.Id).ToList(), today, cancellationToken);
            Func<ApplicationUser, double> key = sort switch
            {
                AdminUserSort.StreakDesc => u => allMetrics[u.Id].Streak,
                AdminUserSort.ConsistencyDesc => u => allMetrics[u.Id].ConsistencyPercent,
                _ => u => allMetrics[u.Id].CalorieAdherencePercent ?? -1,
            };
            page = all.OrderByDescending(key).ThenByDescending(u => u.CreatedAtUtc)
                .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
            pageMetrics = allMetrics;
        }
        else
        {
            filtered = sort switch
            {
                AdminUserSort.JoinedAsc => filtered.OrderBy(u => u.CreatedAtUtc),
                AdminUserSort.LastLoginDesc => filtered.OrderByDescending(u => u.LastLoginAtUtc),
                AdminUserSort.UsernameAsc => filtered.OrderBy(u => u.UserName),
                _ => filtered.OrderByDescending(u => u.CreatedAtUtc),
            };
            page = await filtered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
            pageMetrics = await metrics.ComputeAsync(page.Select(u => u.Id).ToList(), today, cancellationToken);
        }

        var rows = page.Select(u =>
        {
            var m = pageMetrics[u.Id];
            return new AdminUserRow(u.Id, u.UserName ?? string.Empty, u.DisplayName, u.CreatedAtUtc, u.LastLoginAtUtc,
                m.LastActivityAtUtc, AccountLifecycleService.StatusOf(u), m.Goal, m.Streak, m.ConsistencyPercent, m.CalorieAdherencePercent);
        }).ToList();

        return new PagedResult<AdminUserRow>(rows, query.Page, query.PageSize, total);
    }

    [HttpGet("users/{id}")]
    public async Task<ActionResult<AdminUserDetail>> Detail(string id, CancellationToken cancellationToken)
    {
        var (user, error) = await FindManageableAsync(id);
        if (user is null)
        {
            return error!;
        }

        var today = ApiDate.UtcToday;
        var month = await history.GetSummaryAsync(user.Id, today, AdminMetricsService.WindowDays, cancellationToken);
        var twoWeeks = await history.GetSummaryAsync(user.Id, today, 14, cancellationToken);
        var userMetrics = (await metrics.ComputeAsync([user.Id], today, cancellationToken))[user.Id];
        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);

        await audit.WriteAsync(AuditActions.ViewUser, user.Id, new { username = user.UserName }, cancellationToken);

        return new AdminUserDetail(
            user.Id,
            user.UserName ?? string.Empty,
            user.DisplayName,
            AccountLifecycleService.StatusOf(user),
            user.CreatedAtUtc,
            user.LastLoginAtUtc,
            userMetrics.LastActivityAtUtc,
            user.DeletedAtUtc,
            user.DeletionScheduledForUtc,
            profile is null ? null : ProfileDto.From(profile),
            profile is null ? null : TargetCalculator.Calculate(profile),
            month.Streak,
            month.Adherence,
            month.Weights,
            month.Weights.Count >= 2 ? Math.Round(month.Weights[^1].WeightKg - month.Weights[0].WeightKg, 1) : null,
            twoWeeks.Daily,
            await WorkoutComplianceAsync(user.Id, today, cancellationToken),
            user.EmailConfirmed,
            user.MustChangePassword);
    }

    [HttpGet("users/{id}/meals")]
    public async Task<ActionResult<DayMealsResponse>> Meals(string id, [FromQuery] string? date, CancellationToken cancellationToken)
    {
        if (!ApiDate.TryParse(date, out var day))
        {
            return InvalidField("date", ApiDate.InvalidMessage);
        }

        var (user, error) = await FindManageableAsync(id);
        if (user is null)
        {
            return error!;
        }

        var meals = await db.MealEntries.AsNoTracking()
            .Where(m => m.UserId == user.Id && m.Date == day)
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        await audit.WriteAsync(AuditActions.ViewMeals, user.Id, new { username = user.UserName, date = day }, cancellationToken);

        return new DayMealsResponse(day, meals.Select(MealDto.From).ToList(), new MealTotals(
            NutritionMath.Round1(meals.Sum(m => m.Calories)),
            NutritionMath.Round1(meals.Sum(m => m.ProteinG)),
            NutritionMath.Round1(meals.Sum(m => m.CarbsG)),
            NutritionMath.Round1(meals.Sum(m => m.FatG))));
    }

    /// <summary>Everything stored about the user, for data-access requests.</summary>
    [HttpGet("users/{id}/export")]
    public async Task<IActionResult> Export(string id, CancellationToken cancellationToken)
    {
        var (user, error) = await FindManageableAsync(id);
        if (user is null)
        {
            return error!;
        }

        var data = await exporter.ExportAsync(user.Id, cancellationToken);
        await audit.WriteAsync(AuditActions.ExportUser, user.Id, new { username = user.UserName }, cancellationToken);
        Response.Headers.ContentDisposition = $"attachment; filename=himma-export-{user.UserName}-{ApiDate.UtcToday:yyyy-MM-dd}.json";
        return Ok(data);
    }

    private async Task<WorkoutCompliance> WorkoutComplianceAsync(string userId, DateOnly today, CancellationToken cancellationToken)
    {
        var activePlan = await db.WorkoutPlans.AsNoTracking()
            .Where(p => p.UserId == userId && p.IsActive)
            .Select(p => new { p.Name, TrainingDays = p.Days.Count(d => !d.IsRest) })
            .FirstOrDefaultAsync(cancellationToken);

        var windowStart = today.AddDays(-(AdminMetricsService.WindowDays - 1));
        var completed = await db.WorkoutSessions.AsNoTracking()
            .CountAsync(s => s.UserId == userId && s.CompletedAtUtc != null && s.Date >= windowStart && s.Date <= today, cancellationToken);

        var perWeek = activePlan?.TrainingDays ?? 0;
        var planned = (int)Math.Round(perWeek * AdminMetricsService.WindowDays / 7.0);
        return new WorkoutCompliance(
            activePlan?.Name,
            perWeek,
            completed,
            planned,
            planned > 0 ? Math.Min(100, Math.Round(100.0 * completed / planned, 1)) : null);
    }
}
