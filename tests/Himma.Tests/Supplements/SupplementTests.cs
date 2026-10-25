using System.Net;
using System.Net.Http.Json;
using Himma.Api.Features.Meals;
using Himma.Api.Features.Supplements;
using Himma.Tests.Admin;
using Himma.Tests.Auth;
using Himma.Tests.Tracking;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Himma.Tests.Supplements;

internal static class SupplementApi
{
    public static Task<HttpResponseMessage> CreateAsync(HttpClient client, string name = "كرياتين", string[]? times = null,
        string[]? days = null, double? proteinG = null, double? calories = null, string unit = "gram", double dose = 5) =>
        client.PostAsJsonAsync("/api/supplements", new
        {
            name,
            dose,
            unit,
            times = times ?? ["08:00"],
            days = days ?? ["sat", "sun", "mon", "tue", "wed", "thu", "fri"],
            proteinG,
            calories,
        });

    public static async Task<SupplementDto> CreateDtoAsync(HttpClient client, string name = "كرياتين", string[]? times = null,
        double? proteinG = null, double? calories = null)
    {
        var response = await CreateAsync(client, name, times, proteinG: proteinG, calories: calories);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SupplementDto>(TestJson.Options))!;
    }

    public static Task<HttpResponseMessage> TakeAsync(HttpClient client, int id, string? date = null, bool force = false) =>
        client.PostAsJsonAsync($"/api/supplements/{id}/intakes", new { date = date ?? UserApi.Today, force });

    public static async Task<SupplementDayResponse> DayAsync(HttpClient client, string? date = null) =>
        (await client.GetFromJsonAsync<SupplementDayResponse>($"/api/supplements/day/{date ?? UserApi.Today}", TestJson.Options))!;
}

public sealed class SupplementTests(HimmaApiFactory factory) : IClassFixture<HimmaApiFactory>
{
    [Fact]
    public async Task A_supplement_is_saved_and_shows_what_is_due_today()
    {
        var user = await factory.SignUpAsync();

        var created = await SupplementApi.CreateDtoAsync(user.Client, "أوميغا 3", times: ["21:00", "08:30"]);

        Assert.Equal(["08:30", "21:00"], created.Times);
        var item = Assert.Single((await SupplementApi.DayAsync(user.Client)).Items);
        Assert.Equal(created.Id, item.Supplement.Id);
        Assert.True(item.Scheduled);
        Assert.Equal(2, item.DosesDue);
        Assert.Empty(item.Intakes);
        Assert.Equal(0, item.Streak);
    }

    [Fact]
    public async Task Logging_twice_needs_a_confirmation_so_a_double_tap_never_doubles_the_dose()
    {
        var user = await factory.SignUpAsync();
        var creatine = await SupplementApi.CreateDtoAsync(user.Client);

        var first = await SupplementApi.TakeAsync(user.Client, creatine.Id);
        var second = await SupplementApi.TakeAsync(user.Client, creatine.Id);
        var forced = await SupplementApi.TakeAsync(user.Client, creatine.Id, force: true);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await second.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.True(problem!.Extensions.ContainsKey("takenAtUtc"));
        Assert.Equal(HttpStatusCode.Created, forced.StatusCode);

        var item = Assert.Single((await SupplementApi.DayAsync(user.Client)).Items);
        Assert.Equal(2, item.Intakes.Count);
        Assert.Equal(1, item.Streak);
    }

    [Fact]
    public async Task Protein_powder_adds_to_the_days_meals_and_undo_removes_both()
    {
        var user = await factory.SignUpAsync();
        var whey = await SupplementApi.CreateDtoAsync(user.Client, "بروتين باودر", proteinG: 24, calories: 120);

        var intake = await (await SupplementApi.TakeAsync(user.Client, whey.Id)).Content.ReadFromJsonAsync<IntakeDto>(TestJson.Options);
        var meals = await user.Client.GetFromJsonAsync<DayMealsResponse>($"/api/meals/{UserApi.Today}", TestJson.Options);
        var meal = Assert.Single(meals!.Items);
        Assert.Equal("بروتين باودر", meal.Name);
        Assert.Equal(24, meal.ProteinG);
        Assert.Equal(120, meal.Calories);

        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.DeleteAsync($"/api/supplements/intakes/{intake!.Id}")).StatusCode);
        var after = await user.Client.GetFromJsonAsync<DayMealsResponse>($"/api/meals/{UserApi.Today}", TestJson.Options);
        Assert.Empty(after!.Items);
        Assert.Empty(Assert.Single((await SupplementApi.DayAsync(user.Client)).Items).Intakes);
    }

    [Fact]
    public async Task Deleting_a_supplement_removes_its_history_but_keeps_meals_already_eaten()
    {
        var user = await factory.SignUpAsync();
        var whey = await SupplementApi.CreateDtoAsync(user.Client, "واي", proteinG: 25);
        await SupplementApi.TakeAsync(user.Client, whey.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.DeleteAsync($"/api/supplements/{whey.Id}")).StatusCode);

        Assert.Empty((await SupplementApi.DayAsync(user.Client)).Items);
        var meals = await user.Client.GetFromJsonAsync<DayMealsResponse>($"/api/meals/{UserApi.Today}", TestJson.Options);
        Assert.Single(meals!.Items);
        Assert.Equal(0, await AdminApi.WithDbAsync(factory, db => db.SupplementIntakes.CountAsync(i => i.SupplementId == whey.Id)));
    }

    [Fact]
    public async Task Updating_changes_times_and_days()
    {
        var user = await factory.SignUpAsync();
        var creatine = await SupplementApi.CreateDtoAsync(user.Client);

        var updated = await user.Client.PutAsJsonAsync($"/api/supplements/{creatine.Id}", new
        {
            name = "كرياتين مونوهيدرات",
            dose = 3.5,
            unit = "gram",
            times = Array.Empty<string>(),
            days = new[] { "sat", "mon" },
            notes = "مع الفطور",
        });

        var dto = await updated.Content.ReadFromJsonAsync<SupplementDto>(TestJson.Options);
        Assert.Equal("كرياتين مونوهيدرات", dto!.Name);
        Assert.Equal(3.5, dto.Dose);
        Assert.Empty(dto.Times);
        Assert.Equal(2, dto.Days.Count);
        Assert.Equal("مع الفطور", dto.Notes);
    }

    [Theory]
    [InlineData("times", new[] { "25:00" })]
    [InlineData("times", new[] { "8:00" })]
    [InlineData("times", new[] { "08:00", "08:00" })]
    [InlineData("times", new[] { "01:00", "02:00", "03:00", "04:00", "05:00", "06:00", "07:00" })]
    [InlineData("days", new string[0])]
    [InlineData("days", new[] { "someday" })]
    public async Task Invalid_times_and_days_are_rejected(string field, string[] value)
    {
        var user = await factory.SignUpAsync();
        var body = new Dictionary<string, object?>
        {
            ["name"] = "كرياتين", ["dose"] = 5, ["unit"] = "gram", ["times"] = new[] { "08:00" }, ["days"] = new[] { "sat" },
        };
        body[field] = value;

        Assert.Equal(HttpStatusCode.BadRequest, (await user.Client.PostAsJsonAsync("/api/supplements", body)).StatusCode);
    }

    [Theory]
    [InlineData("name", "")]
    [InlineData("dose", 0)]
    [InlineData("unit", "spoon")]
    [InlineData("proteinG", 500)]
    [InlineData("userId", "someone-else")]   // unknown field
    public async Task Invalid_supplement_details_are_rejected(string field, object value)
    {
        var user = await factory.SignUpAsync();
        var body = new Dictionary<string, object?>
        {
            ["name"] = "كرياتين", ["dose"] = 5, ["unit"] = "gram", ["times"] = new[] { "08:00" }, ["days"] = new[] { "sat" },
        };
        body[field] = value;

        Assert.Equal(HttpStatusCode.BadRequest, (await user.Client.PostAsJsonAsync("/api/supplements", body)).StatusCode);
    }

    [Fact]
    public async Task Meals_cannot_be_created_with_the_supplement_source()
    {
        var user = await factory.SignUpAsync();

        var response = await user.Client.PostAsJsonAsync("/api/meals", new
        {
            date = UserApi.Today, mealType = "snack", source = "supplement", name = "x", calories = 100, proteinG = 10,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Supplements_of_another_user_cannot_be_seen_changed_taken_or_undone()
    {
        var a = await factory.SignUpAsync();
        var b = await factory.SignUpAsync();
        var creatine = await SupplementApi.CreateDtoAsync(a.Client);
        var intake = await (await SupplementApi.TakeAsync(a.Client, creatine.Id)).Content.ReadFromJsonAsync<IntakeDto>(TestJson.Options);

        Assert.Empty((await SupplementApi.DayAsync(b.Client)).Items);
        Assert.Empty((await b.Client.GetFromJsonAsync<List<SupplementDto>>("/api/supplements", TestJson.Options))!);
        Assert.Equal(HttpStatusCode.NotFound, (await SupplementApi.TakeAsync(b.Client, creatine.Id, force: true)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Client.DeleteAsync($"/api/supplements/intakes/{intake!.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Client.DeleteAsync($"/api/supplements/{creatine.Id}")).StatusCode);
        var update = await b.Client.PutAsJsonAsync($"/api/supplements/{creatine.Id}", new
        {
            name = "x", dose = 1, unit = "gram", times = Array.Empty<string>(), days = new[] { "sat" },
        });
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);

        var aItem = Assert.Single((await SupplementApi.DayAsync(a.Client)).Items);
        Assert.Equal("كرياتين", aItem.Supplement.Name);
        Assert.Single(aItem.Intakes);
    }

    [Fact]
    public async Task Supplement_endpoints_need_a_signed_in_user()
    {
        var anonymous = factory.CreateHttpsClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/supplements")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/supplements/day/{UserApi.Today}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SupplementApi.TakeAsync(anonymous, 1)).StatusCode);
    }

    // ---------- "Taken" from a notification ----------

    [Fact]
    public async Task The_notification_token_logs_that_one_dose_once()
    {
        var user = await factory.SignUpAsync();
        var creatine = await SupplementApi.CreateDtoAsync(user.Client);
        var token = factory.Services.GetRequiredService<TakeTokens>()
            .Create(user.Session.UserId, creatine.Id, DateOnly.Parse(UserApi.Today));
        var anonymous = factory.CreateHttpsClient();

        var first = await anonymous.PostAsJsonAsync("/api/supplements/take", new { token });
        var again = await anonymous.PostAsJsonAsync("/api/supplements/take", new { token });

        Assert.False((await first.Content.ReadFromJsonAsync<TakeFromNotificationResponse>(TestJson.Options))!.AlreadyTaken);
        Assert.True((await again.Content.ReadFromJsonAsync<TakeFromNotificationResponse>(TestJson.Options))!.AlreadyTaken);
        Assert.Single(Assert.Single((await SupplementApi.DayAsync(user.Client)).Items).Intakes);
    }

    [Theory]
    [InlineData("not-a-token")]
    [InlineData("CfDJ8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Forged_notification_tokens_are_rejected(string token)
    {
        var anonymous = factory.CreateHttpsClient();

        var response = await anonymous.PostAsJsonAsync("/api/supplements/take", new { token });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_token_for_someone_elses_supplement_does_nothing()
    {
        var a = await factory.SignUpAsync();
        var b = await factory.SignUpAsync();
        var creatine = await SupplementApi.CreateDtoAsync(a.Client);
        // B's user id with A's supplement id: the supplement is looked up for B only.
        var token = factory.Services.GetRequiredService<TakeTokens>()
            .Create(b.Session.UserId, creatine.Id, DateOnly.Parse(UserApi.Today));

        var response = await factory.CreateHttpsClient().PostAsJsonAsync("/api/supplements/take", new { token });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(Assert.Single((await SupplementApi.DayAsync(a.Client)).Items).Intakes);
    }

    [Fact]
    public async Task Purging_an_account_removes_its_supplements_and_doses()
    {
        var admin = await factory.SignInAsAdminAsync();
        var user = await factory.SignUpAsync();
        var whey = await SupplementApi.CreateDtoAsync(user.Client, "واي", proteinG: 25);
        await SupplementApi.TakeAsync(user.Client, whey.Id);

        var purged = await AdminApi.PurgeAsync(admin, user.Session.UserId, factory.AdminPassword, user.Session.Username);

        Assert.Equal(HttpStatusCode.NoContent, purged.StatusCode);
        Assert.Equal(0, await AdminApi.WithDbAsync(factory, db => db.Supplements.CountAsync(s => s.UserId == user.Session.UserId)));
        Assert.Equal(0, await AdminApi.WithDbAsync(factory, db => db.SupplementIntakes.CountAsync(i => i.UserId == user.Session.UserId)));
    }
}
