using System.Net;
using System.Net.Http.Json;
using Himma.Api.Features.Account;
using Himma.Api.Features.Admin;
using Himma.Api.Features.Meals;
using Himma.Api.Infrastructure.Audit;
using Himma.Tests.Auth;
using Himma.Tests.Tracking;
using Himma.Tests.Workouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Himma.Tests.Admin;

public sealed class AdminTests(HimmaApiFactory factory) : IClassFixture<HimmaApiFactory>
{
    // ---------- Access control ----------

    [Theory]
    [InlineData("GET", "/api/admin/overview")]
    [InlineData("GET", "/api/admin/users")]
    [InlineData("GET", "/api/admin/users/some-id")]
    [InlineData("GET", "/api/admin/users/some-id/meals?date=2026-01-01")]
    [InlineData("GET", "/api/admin/users/some-id/export")]
    [InlineData("POST", "/api/admin/users/some-id/suspend")]
    [InlineData("POST", "/api/admin/users/some-id/reactivate")]
    [InlineData("DELETE", "/api/admin/users/some-id")]
    [InlineData("POST", "/api/admin/users/some-id/restore")]
    [InlineData("POST", "/api/admin/users/some-id/purge")]
    [InlineData("GET", "/api/admin/audit-logs")]
    public async Task Admin_endpoints_are_forbidden_for_users_and_require_sign_in(string method, string url)
    {
        var user = await factory.SignUpAsync();
        var anonymous = factory.CreateHttpsClient();

        var asUser = await user.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url) { Content = JsonContent.Create(new { }) });
        var asAnonymous = await anonymous.SendAsync(new HttpRequestMessage(new HttpMethod(method), url) { Content = JsonContent.Create(new { }) });

        Assert.Equal(HttpStatusCode.Forbidden, asUser.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, asAnonymous.StatusCode);
    }

    // ---------- Reading ----------

    [Fact]
    public async Task Overview_and_list_show_subscribers_but_not_admins()
    {
        var admin = await factory.SignInAsAdminAsync();
        var user = await factory.SignUpAsync();
        await UserApi.PutProfileAsync(user.Client);
        await UserApi.AddAiMealAsync(user.Client, UserApi.Today, 2259, 160);

        var overview = await admin.GetFromJsonAsync<AdminOverview>("/api/admin/overview", TestJson.Options);
        var list = await admin.GetFromJsonAsync<PagedResult<AdminUserRow>>(
            $"/api/admin/users?search={user.Session.Username}", TestJson.Options);
        var adminSearch = await admin.GetFromJsonAsync<PagedResult<AdminUserRow>>(
            $"/api/admin/users?search={HimmaApiFactory.AdminUsername}&status=all", TestJson.Options);

        Assert.True(overview!.TotalUsers >= 1);
        Assert.True(overview.NewLast7Days >= 1);
        Assert.True(overview.ActiveLast7Days >= 1);
        Assert.Equal(12, overview.WeeklySignups.Count);
        var row = Assert.Single(list!.Items);
        Assert.Equal(AccountStatus.Active, row.Status);
        Assert.Equal(1, row.Streak);
        Assert.Equal(3.3, row.ConsistencyPercent); // 1 of 30 days
        Assert.Equal(3.3, row.CalorieAdherencePercent);
        Assert.NotNull(row.LastActivityAtUtc);
        Assert.Empty(adminSearch!.Items);
    }

    [Fact]
    public async Task List_supports_paging_sorting_and_validation()
    {
        var admin = await factory.SignInAsAdminAsync();
        await factory.SignUpAsync();
        await factory.SignUpAsync();
        await factory.SignUpAsync();

        var page = await admin.GetFromJsonAsync<PagedResult<AdminUserRow>>("/api/admin/users?pageSize=2&sort=joined_asc", TestJson.Options);
        var byStreak = await admin.GetAsync("/api/admin/users?sort=streak_desc");

        Assert.Equal(2, page!.Items.Count);
        Assert.True(page.TotalCount >= 3);
        Assert.True(page.Items[0].CreatedAtUtc <= page.Items[1].CreatedAtUtc);
        Assert.Equal(HttpStatusCode.OK, byStreak.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/users?sort=evil")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/users?status=hacked")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/users?pageSize=1000")).StatusCode);
    }

    [Fact]
    public async Task Viewing_details_meals_and_export_is_audited()
    {
        var admin = await factory.SignInAsAdminAsync();
        var user = await factory.SignUpAsync();
        await UserApi.PutProfileAsync(user.Client, weightKg: 82, date: UserApi.DaysAgo(5));
        await user.Client.PostAsJsonAsync("/api/weight", new { date = UserApi.Today, weightKg = 80.5 });
        await UserApi.AddAiMealAsync(user.Client, UserApi.Today, 700, 40);
        await (await WorkoutApi.ApplyTemplateAsync(user.Client)).ReadAsync<Himma.Api.Features.Workouts.PlanDetailDto>();
        var id = user.Session.UserId;

        var detail = await admin.GetFromJsonAsync<AdminUserDetail>($"/api/admin/users/{id}", TestJson.Options);
        var meals = await admin.GetFromJsonAsync<DayMealsResponse>($"/api/admin/users/{id}/meals?date={UserApi.Today}", TestJson.Options);
        var export = await admin.GetAsync($"/api/admin/users/{id}/export");

        Assert.Equal(user.Session.Username, detail!.Username);
        Assert.NotNull(detail.Targets);
        Assert.Equal(80.5, detail.Profile!.WeightKg);
        Assert.Equal(30, detail.Adherence30.WindowDays);
        Assert.Equal(1, detail.Streak);
        Assert.Equal(-1.5, detail.WeightChange30Kg);
        Assert.Equal(14, detail.Last14Days.Count);
        Assert.Equal("تقسيم الجيم - 4 أيام", detail.Workouts.ActivePlanName);
        Assert.Equal(4, detail.Workouts.TrainingDaysPerWeek);
        Assert.Equal(17, detail.Workouts.PlannedSessions30);
        Assert.Equal(700, meals!.Totals.Calories);
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Contains("attachment", export.Content.Headers.ContentDisposition?.ToString() ?? export.Headers.ToString());
        var exported = await export.Content.ReadAsStringAsync();
        Assert.Contains(user.Session.Username, exported);
        Assert.DoesNotContain("passwordHash", exported, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", exported, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(1, await AdminApi.AuditCountAsync(factory, AuditActions.ViewUser, id));
        Assert.Equal(1, await AdminApi.AuditCountAsync(factory, AuditActions.ViewMeals, id));
        Assert.Equal(1, await AdminApi.AuditCountAsync(factory, AuditActions.ExportUser, id));
    }

    // ---------- Suspend / reactivate ----------

    [Fact]
    public async Task Suspending_needs_the_admin_password_and_locks_the_user_out_everywhere()
    {
        var admin = await factory.SignInAsAdminAsync();
        var user = await factory.SignUpAsync();
        var id = user.Session.UserId;

        var wrong = await AdminApi.SuspendAsync(admin, id, AuthApi.NewPassword());
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.True((await wrong.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.ContainsKey("adminPassword"));
        Assert.Equal(1, await AdminApi.AuditCountAsync(factory, AuditActions.ReauthFailed, id));

        Assert.Equal(HttpStatusCode.NoContent, (await AdminApi.SuspendAsync(admin, id, factory.AdminPassword)).StatusCode);

        // The user's current token, refresh cookie and password no longer work.
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.Client.GetAsync("/api/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthApi.RefreshAsync(factory.CreateHttpsClient(), user.Session.RefreshCookie)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AuthApi.LoginAsync(factory.CreateHttpsClient(), user.Session.Username, user.Session.Password)).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await AdminApi.SuspendAsync(admin, id, factory.AdminPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/admin/users/{id}/reactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.LoginAsync(factory.CreateHttpsClient(), user.Session.Username, user.Session.Password)).StatusCode);
        Assert.Equal(1, await AdminApi.AuditCountAsync(factory, AuditActions.Suspend, id));
        Assert.Equal(1, await AdminApi.AuditCountAsync(factory, AuditActions.Reactivate, id));
    }

    // ---------- Soft delete / restore / purge ----------

    [Fact]
    public async Task Soft_delete_hides_the_account_and_restore_brings_it_back()
    {
        var admin = await factory.SignInAsAdminAsync();
        var user = await factory.SignUpAsync();
        var id = user.Session.UserId;

        Assert.Equal(HttpStatusCode.NoContent, (await AdminApi.SoftDeleteAsync(admin, id, factory.AdminPassword)).StatusCode);

        var normal = await admin.GetFromJsonAsync<PagedResult<AdminUserRow>>($"/api/admin/users?search={user.Session.Username}", TestJson.Options);
        var pending = await admin.GetFromJsonAsync<PagedResult<AdminUserRow>>($"/api/admin/users?search={user.Session.Username}&status=pendingDeletion", TestJson.Options);
        Assert.Empty(normal!.Items);
        Assert.Equal(AccountStatus.PendingDeletion, Assert.Single(pending!.Items).Status);

        var login = await AuthApi.LoginAsync(factory.CreateHttpsClient(), user.Session.Username, user.Session.Password);
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        var scheduled = await AdminApi.WithDbAsync(factory, db => db.Users.Where(u => u.Id == id).Select(u => u.DeletionScheduledForUtc).SingleAsync());
        Assert.InRange(scheduled!.Value, DateTime.UtcNow.AddDays(29.9), DateTime.UtcNow.AddDays(30.1));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/admin/users/{id}/restore", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.LoginAsync(factory.CreateHttpsClient(), user.Session.Username, user.Session.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/admin/users/{id}/restore", null)).StatusCode);
    }

    [Fact]
    public async Task Purge_needs_the_exact_username_and_password_then_erases_everything_but_the_audit_trail()
    {
        var admin = await factory.SignInAsAdminAsync();
        var user = await factory.SignUpAsync();
        var id = user.Session.UserId;
        await UserApi.PutProfileAsync(user.Client);
        await UserApi.AddAiMealAsync(user.Client, UserApi.Today);
        await UserApi.AddWaterAsync(user.Client, UserApi.Today);
        // Own exercise used in a plan and a finished session: checks the delete cascade end to end.
        var mine = await (await WorkoutApi.CreateExerciseAsync(user.Client)).ReadAsync<Himma.Api.Features.Workouts.ExerciseDto>();
        var plan = await (await WorkoutApi.CreatePlanAsync(user.Client)).ReadAsync<Himma.Api.Features.Workouts.PlanDetailDto>();
        var day = await (await WorkoutApi.PutDayAsync(user.Client, plan.Id, "sat", WorkoutApi.TrainingDay((mine.Id, 3, 10, null))))
            .ReadAsync<Himma.Api.Features.Workouts.PlanDayDto>();
        var session = await (await WorkoutApi.StartSessionAsync(user.Client, UserApi.Today, day.Id)).ReadAsync<Himma.Api.Features.Workouts.SessionDto>();
        await WorkoutApi.SaveSetsAsync(user.Client, session.Id, (mine.Id, 1, 10, 20, true));

        var wrongName = await AdminApi.PurgeAsync(admin, id, factory.AdminPassword, user.Session.Username.ToUpperInvariant());
        Assert.Equal(HttpStatusCode.BadRequest, wrongName.StatusCode);
        Assert.True((await wrongName.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.ContainsKey("confirmUsername"));

        var purged = await AdminApi.PurgeAsync(admin, id, factory.AdminPassword, user.Session.Username);
        Assert.Equal(HttpStatusCode.NoContent, purged.StatusCode);

        var leftovers = await AdminApi.WithDbAsync(factory, async db => new[]
        {
            await db.Users.CountAsync(u => u.Id == id),
            await db.UserProfiles.CountAsync(p => p.UserId == id),
            await db.MealEntries.CountAsync(m => m.UserId == id),
            await db.WaterEntries.CountAsync(w => w.UserId == id),
            await db.WeightEntries.CountAsync(w => w.UserId == id),
            await db.Exercises.CountAsync(e => e.OwnerUserId == id),
            await db.WorkoutPlans.CountAsync(p => p.UserId == id),
            await db.WorkoutSessions.CountAsync(s => s.UserId == id),
            await db.RefreshTokens.CountAsync(t => t.UserId == id),
        });
        Assert.All(leftovers, count => Assert.Equal(0, count));
        Assert.Equal(1, await AdminApi.AuditCountAsync(factory, AuditActions.Purge, id));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/admin/users/{id}")).StatusCode);
    }

    [Fact]
    public async Task Background_job_purges_accounts_whose_grace_period_ended()
    {
        var user = await factory.SignUpAsync();
        var id = user.Session.UserId;
        var admin = await factory.SignInAsAdminAsync();
        await AdminApi.SoftDeleteAsync(admin, id, factory.AdminPassword);
        await AdminApi.WithDbAsync(factory, db => db.Users.Where(u => u.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.DeletionScheduledForUtc, DateTime.UtcNow.AddMinutes(-1))));

        var job = factory.Services.GetServices<IHostedService>().OfType<ScheduledPurgeService>().Single();
        var purged = await job.PurgeExpiredAsync(CancellationToken.None);

        Assert.True(purged >= 1);
        Assert.Equal(0, await AdminApi.WithDbAsync(factory, db => db.Users.CountAsync(u => u.Id == id)));
        Assert.Equal(1, await AdminApi.AuditCountAsync(factory, AuditActions.SystemPurge, id));
    }

    // ---------- Protection of admins ----------

    [Fact]
    public async Task Admins_cannot_act_on_themselves_or_other_admins()
    {
        var admin = await factory.SignInAsAdminAsync();
        var adminId = await AdminApi.WithDbAsync(factory, db => db.Users.Where(u => u.UserName == HimmaApiFactory.AdminUsername).Select(u => u.Id).SingleAsync());
        var other = await factory.SignUpAsync();
        await AdminApi.PromoteToAdminAsync(factory, other.Session.UserId);

        Assert.Equal(HttpStatusCode.Forbidden, (await AdminApi.SuspendAsync(admin, adminId, factory.AdminPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AdminApi.SoftDeleteAsync(admin, adminId, factory.AdminPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AdminApi.SuspendAsync(admin, other.Session.UserId, factory.AdminPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AdminApi.PurgeAsync(admin, other.Session.UserId, factory.AdminPassword, other.Session.Username)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await AdminApi.SuspendAsync(admin, "no-such-user", factory.AdminPassword)).StatusCode);
    }

    // ---------- Audit log ----------

    [Fact]
    public async Task Audit_log_can_be_filtered_and_shows_usernames()
    {
        var admin = await factory.SignInAsAdminAsync();
        var user = await factory.SignUpAsync();
        await admin.GetAsync($"/api/admin/users/{user.Session.UserId}");

        var logs = await admin.GetFromJsonAsync<PagedResult<AuditLogRow>>(
            $"/api/admin/audit-logs?target={user.Session.Username}&actor={HimmaApiFactory.AdminUsername}&action=admin.view", TestJson.Options);

        var row = Assert.Single(logs!.Items);
        Assert.Equal(AuditActions.ViewUser, row.Action);
        Assert.Equal(HimmaApiFactory.AdminUsername, row.ActorUsername);
        Assert.Equal(user.Session.Username, row.TargetUsername);
        Assert.Equal("Admin", row.ActorRole);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/audit-logs?from=yesterday")).StatusCode);
    }
}
