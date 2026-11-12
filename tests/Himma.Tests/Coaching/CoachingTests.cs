using System.Net;
using System.Net.Http.Json;
using Himma.Api.Domain.Entities;
using Himma.Api.Features.Coaching;
using Himma.Api.Features.Meals;
using Himma.Api.Features.Workouts;
using Himma.Api.Infrastructure.Audit;
using Himma.Tests.Admin;
using Himma.Tests.Auth;
using Himma.Tests.Tracking;
using Himma.Tests.Workouts;
using Microsoft.EntityFrameworkCore;

namespace Himma.Tests.Coaching;

public sealed class CoachingTests(HimmaApiFactory factory) : IClassFixture<HimmaApiFactory>
{
    private static string InDays(int days) => DateTime.UtcNow.AddDays(days).ToString("yyyy-MM-dd");

    /// <summary>A signed-up user with a coach package switched on by the admin.</summary>
    private async Task<(SignedInUser Coach, CoachAccountDto Account)> NewCoachAsync(int seats = 10, int days = 30)
    {
        var admin = await factory.SignInAsAdminAsync();
        var coach = await factory.SignUpAsync();
        var set = await admin.PutAsJsonAsync($"/api/admin/users/{coach.Session.UserId}/coach",
            new { businessName = "نادي القوة", maxClients = seats, activeUntil = InDays(days) });
        set.EnsureSuccessStatusCode();
        var account = await coach.Client.GetFromJsonAsync<CoachAccountDto>("/api/coach/me", TestJson.Options);
        return (coach, account!);
    }

    private async Task<SignedInUser> NewClientOfAsync(CoachAccountDto account)
    {
        var client = await factory.SignUpAsync();
        var join = await client.Client.PostAsJsonAsync("/api/my-coach/join", new { code = account.InviteCode });
        join.EnsureSuccessStatusCode();
        return client;
    }

    // ---------- Access ----------

    [Theory]
    [InlineData("GET", "/api/coach/clients")]
    [InlineData("GET", "/api/coach/clients/x")]
    [InlineData("PUT", "/api/coach/me")]
    [InlineData("POST", "/api/coach/me/invite-code")]
    public async Task Coach_endpoints_need_a_package(string method, string url)
    {
        var user = await factory.SignUpAsync();
        var anonymous = factory.CreateHttpsClient();

        var asUser = await user.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url) { Content = JsonContent.Create(new { businessName = "x" }) });
        var asAnonymous = await anonymous.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        Assert.Equal(HttpStatusCode.NotFound, asUser.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, asAnonymous.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.GetAsync("/api/coach/me")).StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/admin/coaches")]
    [InlineData("GET", "/api/admin/users/x/coach")]
    [InlineData("PUT", "/api/admin/users/x/coach")]
    [InlineData("DELETE", "/api/admin/users/x/coach")]
    public async Task Only_the_admin_sells_packages(string method, string url)
    {
        var (coach, _) = await NewCoachAsync();
        var response = await coach.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = JsonContent.Create(new { businessName = "x", maxClients = 500, activeUntil = InDays(999) }),
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_give_a_package_to_an_admin_and_validates_input()
    {
        var admin = await factory.SignInAsAdminAsync();
        var adminId = await AdminApi.WithDbAsync(factory, db => db.Users.Where(u => u.UserName == HimmaApiFactory.AdminUsername).Select(u => u.Id).SingleAsync());
        var user = await factory.SignUpAsync();

        var bad = await admin.PutAsJsonAsync($"/api/admin/users/{user.Session.UserId}/coach",
            new { businessName = "", maxClients = 0, activeUntil = "tomorrow" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var self = await admin.PutAsJsonAsync($"/api/admin/users/{adminId}/coach",
            new { businessName = "x", maxClients = 5, activeUntil = InDays(10) });
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
    }

    // ---------- Joining ----------

    [Fact]
    public async Task Client_joins_with_code_and_coach_sees_them_with_attention_flags()
    {
        var (coach, account) = await NewCoachAsync();
        Assert.True(account.IsActive);
        Assert.Equal(8, account.InviteCode.Length);

        var client = await factory.SignUpAsync();
        // Codes are forgiving about case, spaces and dashes.
        var messy = $" {account.InviteCode[..4].ToLowerInvariant()}-{account.InviteCode[4..]} ";
        var preview = await client.Client.GetFromJsonAsync<CoachPublicDto>($"/api/my-coach/preview?code={Uri.EscapeDataString(messy)}", TestJson.Options);
        Assert.Equal("نادي القوة", preview!.BusinessName);

        var joined = await (await client.Client.PostAsJsonAsync("/api/my-coach/join", new { code = messy })).ReadAsync<MyCoachResponse>();
        Assert.Equal("نادي القوة", joined.Coach!.BusinessName);

        var list = await coach.Client.GetFromJsonAsync<CoachClientsResponse>("/api/coach/clients", TestJson.Options);
        var row = Assert.Single(list!.Clients);
        Assert.Equal(client.Session.UserId, row.Id);
        Assert.Contains(AttentionReasons.Inactive, row.Attention);
        Assert.Contains(AttentionReasons.NoProfile, row.Attention);
        Assert.Equal(1, list.Coach.ActiveClients);
        Assert.Equal(1, await AdminApi.AuditCountAsync(factory, AuditActions.JoinCoach, coach.Session.UserId));
    }

    [Fact]
    public async Task Active_client_with_good_days_is_not_flagged()
    {
        var (coach, account) = await NewCoachAsync();
        var client = await NewClientOfAsync(account);
        await UserApi.PutProfileAsync(client.Client);
        await UserApi.AddAiMealAsync(client.Client, UserApi.Today, 2259, 160);

        var list = await coach.Client.GetFromJsonAsync<CoachClientsResponse>("/api/coach/clients", TestJson.Options);
        var row = Assert.Single(list!.Clients);
        Assert.DoesNotContain(AttentionReasons.Inactive, row.Attention);
        Assert.DoesNotContain(AttentionReasons.NoProfile, row.Attention);
        // Joined today and on target today: on track, even though it is 1 day of the 7-day window.
        Assert.DoesNotContain(AttentionReasons.LowCalories, row.Attention);
        Assert.DoesNotContain(AttentionReasons.LowProtein, row.Attention);
        Assert.Contains(AttentionReasons.LowWater, row.Attention); // no water logged
        Assert.Equal(1, row.Streak);
    }

    [Theory]
    [InlineData(0, 1, 100, false)]   // joined today, on target today
    [InlineData(10, 2, 28.6, true)]  // a regular client on target 2 of the last 7 days
    [InlineData(10, 5, 71.4, false)]
    [InlineData(2, 2, 28.6, false)]  // joined 2 days ago (3 days with the coach), on target 2 of them
    public void Low_adherence_counts_only_the_days_with_the_coach(int joinedDaysAgo, int _, double percent, bool flagged)
    {
        var now = DateTime.UtcNow;
        var metrics = new Himma.Api.Features.Admin.UserMetrics(1, 50, percent, 100, 100, now, Himma.Api.Domain.Goal.Lose);
        var reasons = CoachingService.Attention(metrics, now, now.AddDays(-joinedDaysAgo), now);
        Assert.Equal(flagged, reasons.Contains(AttentionReasons.LowCalories));
    }

    [Fact]
    public async Task Wrong_or_expired_code_and_full_seats_are_refused()
    {
        var user = await factory.SignUpAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.PostAsJsonAsync("/api/my-coach/join", new { code = "ZZZZZZZZ" })).StatusCode);

        var (_, expired) = await NewCoachAsync(days: -1);
        Assert.False(expired.IsActive);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.PostAsJsonAsync("/api/my-coach/join", new { code = expired.InviteCode })).StatusCode);

        var (_, small) = await NewCoachAsync(seats: 1);
        await NewClientOfAsync(small);
        var full = await user.Client.PostAsJsonAsync("/api/my-coach/join", new { code = small.InviteCode });
        Assert.Equal(HttpStatusCode.Conflict, full.StatusCode);
    }

    [Fact]
    public async Task A_client_has_one_coach_at_a_time_and_cannot_join_themselves()
    {
        var (coach, first) = await NewCoachAsync();
        var (_, second) = await NewCoachAsync();
        var client = await NewClientOfAsync(first);

        Assert.Equal(HttpStatusCode.Conflict, (await client.Client.PostAsJsonAsync("/api/my-coach/join", new { code = second.InviteCode })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await coach.Client.PostAsJsonAsync("/api/my-coach/join", new { code = first.InviteCode })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.Client.DeleteAsync("/api/my-coach")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.Client.PostAsJsonAsync("/api/my-coach/join", new { code = second.InviteCode })).StatusCode);
    }

    [Fact]
    public async Task New_invite_code_stops_the_old_one_but_keeps_clients()
    {
        var (coach, account) = await NewCoachAsync();
        await NewClientOfAsync(account);

        var renewed = await (await coach.Client.PostAsync("/api/coach/me/invite-code", null)).ReadAsync<CoachAccountDto>();
        Assert.NotEqual(account.InviteCode, renewed.InviteCode);
        Assert.Equal(1, renewed.ActiveClients);

        var other = await factory.SignUpAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.PostAsJsonAsync("/api/my-coach/join", new { code = account.InviteCode })).StatusCode);
    }

    // ---------- Privacy ----------

    [Fact]
    public async Task Coach_only_sees_own_clients_and_loses_access_when_client_leaves()
    {
        var (coach, account) = await NewCoachAsync();
        var (otherCoach, _) = await NewCoachAsync();
        var client = await NewClientOfAsync(account);
        var stranger = await factory.SignUpAsync();
        var url = $"/api/coach/clients/{client.Session.UserId}";

        Assert.Equal(HttpStatusCode.OK, (await coach.Client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherCoach.Client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coach.Client.GetAsync($"/api/coach/clients/{stranger.Session.UserId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coach.Client.GetAsync($"/api/coach/clients/{stranger.Session.UserId}/meals?date={UserApi.Today}")).StatusCode);
        Assert.Equal(1, await AdminApi.AuditCountAsync(factory, AuditActions.CoachViewClient, client.Session.UserId));

        await client.Client.DeleteAsync("/api/my-coach");
        Assert.Equal(HttpStatusCode.NotFound, (await coach.Client.GetAsync(url)).StatusCode);
        var list = await coach.Client.GetFromJsonAsync<CoachClientsResponse>("/api/coach/clients", TestJson.Options);
        Assert.Empty(list!.Clients);
    }

    [Fact]
    public async Task Expired_package_blocks_the_dashboard_until_renewed()
    {
        var admin = await factory.SignInAsAdminAsync();
        var (coach, account) = await NewCoachAsync();
        var client = await NewClientOfAsync(account);

        await admin.PutAsJsonAsync($"/api/admin/users/{coach.Session.UserId}/coach", new { businessName = "نادي القوة", maxClients = 10, activeUntil = InDays(-2) });
        Assert.Equal(HttpStatusCode.Forbidden, (await coach.Client.GetAsync("/api/coach/clients")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await coach.Client.GetAsync($"/api/coach/clients/{client.Session.UserId}")).StatusCode);

        await admin.PutAsJsonAsync($"/api/admin/users/{coach.Session.UserId}/coach", new { businessName = "نادي القوة", maxClients = 10, activeUntil = InDays(30) });
        var list = await coach.Client.GetFromJsonAsync<CoachClientsResponse>("/api/coach/clients", TestJson.Options);
        Assert.Single(list!.Clients);
    }

    [Fact]
    public async Task Removing_the_package_ends_every_link()
    {
        var admin = await factory.SignInAsAdminAsync();
        var (coach, account) = await NewCoachAsync();
        var client = await NewClientOfAsync(account);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/users/{coach.Session.UserId}/coach")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await coach.Client.GetAsync("/api/coach/me")).StatusCode);
        var mine = await client.Client.GetFromJsonAsync<MyCoachResponse>("/api/my-coach", TestJson.Options);
        Assert.Null(mine!.Coach);
        Assert.Equal(1, await AdminApi.AuditCountAsync(factory, AuditActions.RemoveCoach, coach.Session.UserId));
    }

    [Fact]
    public async Task Private_note_is_never_shown_to_the_client()
    {
        var (coach, account) = await NewCoachAsync();
        var client = await NewClientOfAsync(account);

        var put = await coach.Client.PutAsJsonAsync($"/api/coach/clients/{client.Session.UserId}/note", new { note = "ركبته تؤلمه، لا سكوات ثقيل" });
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var detail = await coach.Client.GetFromJsonAsync<CoachClientDetail>($"/api/coach/clients/{client.Session.UserId}", TestJson.Options);
        Assert.Equal("ركبته تؤلمه، لا سكوات ثقيل", detail!.PrivateNote);

        var raw = await client.Client.GetStringAsync("/api/my-coach");
        Assert.DoesNotContain("ركبته", raw);
        var export = await client.Client.GetStringAsync("/api/account/export");
        Assert.DoesNotContain("ركبته", export);
    }

    // ---------- Working with clients ----------

    [Fact]
    public async Task Coach_reads_client_meals()
    {
        var (coach, account) = await NewCoachAsync();
        var client = await NewClientOfAsync(account);
        await UserApi.AddAiMealAsync(client.Client, UserApi.Today, 600, 40);

        var day = await coach.Client.GetFromJsonAsync<DayMealsResponse>(
            $"/api/coach/clients/{client.Session.UserId}/meals?date={UserApi.Today}", TestJson.Options);
        Assert.Equal(600, day!.Totals.Calories);
        Assert.Equal(1, await AdminApi.AuditCountAsync(factory, AuditActions.CoachViewMeals, client.Session.UserId));
    }

    [Fact]
    public async Task Messages_reach_the_client_with_a_push_and_can_be_marked_read()
    {
        var (coach, account) = await NewCoachAsync();
        var client = await NewClientOfAsync(account);
        var endpoint = $"https://push.example/{Guid.NewGuid():N}";
        await AdminApi.WithDbAsync(factory, async db =>
        {
            db.PushDevices.Add(new PushDevice { UserId = client.Session.UserId, Endpoint = endpoint, P256dh = "k", Auth = "a", CreatedAtUtc = DateTime.UtcNow });
            return await db.SaveChangesAsync();
        });

        var sent = await (await coach.Client.PostAsJsonAsync($"/api/coach/clients/{client.Session.UserId}/messages",
            new { text = "ممتاز هالأسبوع! زد الماء شوي" })).ReadAsync<CoachNoteDto>();
        Assert.Null(sent.ReadAtUtc);

        var push = Assert.Single(factory.Push.To(endpoint));
        Assert.Equal("رسالة من نادي القوة", push.Title);
        Assert.Equal("/#coach", push.Url);

        var mine = await client.Client.GetFromJsonAsync<MyCoachResponse>("/api/my-coach", TestJson.Options);
        Assert.Equal(1, mine!.UnreadCount);
        Assert.Equal("ممتاز هالأسبوع! زد الماء شوي", mine.Messages[0].Text);

        await client.Client.PostAsync("/api/my-coach/messages/read", null);
        mine = await client.Client.GetFromJsonAsync<MyCoachResponse>("/api/my-coach", TestJson.Options);
        Assert.Equal(0, mine!.UnreadCount);

        var empty = await coach.Client.PostAsJsonAsync($"/api/coach/clients/{client.Session.UserId}/messages", new { text = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task Coach_sends_a_plan_copy_including_own_exercises()
    {
        var (coach, account) = await NewCoachAsync();
        var client = await NewClientOfAsync(account);
        await WorkoutApi.CreatePlanAsync(client.Client, "خطتي القديمة");

        var custom = await (await WorkoutApi.CreateExerciseAsync(coach.Client, "ضغط المدرب")).ReadAsync<ExerciseDto>();
        var system = await WorkoutApi.SystemExerciseAsync(coach.Client, Himma.Api.Data.Seed.ExerciseSeedData.FlatBenchPress);
        var plan = await (await WorkoutApi.CreatePlanAsync(coach.Client, "برنامج التضخيم")).ReadAsync<PlanDetailDto>();
        (await WorkoutApi.PutDayAsync(coach.Client, plan.Id, "sat", WorkoutApi.TrainingDay((custom.Id, 4, 10, 20), (system.Id, 3, 8, 60)))).EnsureSuccessStatusCode();

        var assigned = await (await coach.Client.PostAsJsonAsync($"/api/coach/clients/{client.Session.UserId}/plans",
            new { planId = plan.Id, activate = true })).ReadAsync<AssignPlanResponse>();
        Assert.True(assigned.IsActive);

        var copy = await client.Client.GetFromJsonAsync<PlanDetailDto>($"/api/plans/{assigned.PlanId}", TestJson.Options);
        Assert.Equal("برنامج التضخيم", copy!.Name);
        var items = copy.Days.Single(d => d.DayKey == Himma.Api.Domain.DayKey.Sat).Items;
        Assert.Equal(2, items.Count);
        Assert.NotEqual(custom.Id, items[0].Exercise.Id); // the client got their own copy of the coach's exercise
        Assert.Equal(system.Id, items[1].Exercise.Id);

        var plans = await client.Client.GetFromJsonAsync<List<PlanSummaryDto>>("/api/plans", TestJson.Options);
        Assert.Single(plans!, p => p.IsActive);

        // Plans that are not the coach's own cannot be sent.
        var clientPlan = plans!.First(p => p.Name == "خطتي القديمة");
        var stolen = await coach.Client.PostAsJsonAsync($"/api/coach/clients/{client.Session.UserId}/plans", new { planId = clientPlan.Id, activate = false });
        Assert.Equal(HttpStatusCode.NotFound, stolen.StatusCode);

        // The copy outlives the coach's exercise and the link.
        await client.Client.DeleteAsync("/api/my-coach");
        Assert.Equal(HttpStatusCode.OK, (await client.Client.GetAsync($"/api/plans/{assigned.PlanId}")).StatusCode);
    }

    [Fact]
    public async Task Purging_a_client_or_coach_removes_their_links_and_messages()
    {
        var admin = await factory.SignInAsAdminAsync();
        var (coach, account) = await NewCoachAsync();
        var client = await NewClientOfAsync(account);
        await coach.Client.PostAsJsonAsync($"/api/coach/clients/{client.Session.UserId}/messages", new { text = "أهلاً" });

        var purge = await AdminApi.PurgeAsync(admin, client.Session.UserId, factory.AdminPassword, client.Session.Username);
        Assert.Equal(HttpStatusCode.NoContent, purge.StatusCode);
        Assert.Equal(0, await AdminApi.WithDbAsync(factory, db => db.CoachClients.CountAsync(l => l.ClientUserId == client.Session.UserId)));
        Assert.Equal(0, await AdminApi.WithDbAsync(factory, db => db.CoachNotes.CountAsync(n => n.ClientUserId == client.Session.UserId)));

        var second = await NewClientOfAsync(account);
        purge = await AdminApi.PurgeAsync(admin, coach.Session.UserId, factory.AdminPassword, coach.Session.Username);
        Assert.Equal(HttpStatusCode.NoContent, purge.StatusCode);
        var mine = await second.Client.GetFromJsonAsync<MyCoachResponse>("/api/my-coach", TestJson.Options);
        Assert.Null(mine!.Coach);
    }
}
