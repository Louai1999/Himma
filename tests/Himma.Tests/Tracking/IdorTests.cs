using System.Net;
using System.Net.Http.Json;
using Himma.Api.Features.Foods;
using Himma.Api.Features.History;
using Himma.Api.Features.Meals;
using Himma.Api.Features.Water;
using Himma.Api.Features.Weight;

namespace Himma.Tests.Tracking;

/// <summary>
/// User B must never see, change or use anything that belongs to user A,
/// even when B knows A's ids. Ids of other users behave exactly like ids that do not exist.
/// </summary>
public sealed class IdorTests(HimmaApiFactory factory) : IClassFixture<HimmaApiFactory>
{
    [Fact]
    public async Task Meals_of_another_user_cannot_be_read_or_deleted()
    {
        var a = await factory.SignUpAsync();
        var b = await factory.SignUpAsync();
        var mealId = await UserApi.IdOfAsync(await UserApi.AddAiMealAsync(a.Client, UserApi.Today));

        var bDay = await b.Client.GetFromJsonAsync<DayMealsResponse>($"/api/meals/{UserApi.Today}", TestJson.Options);
        var bDelete = await b.Client.DeleteAsync($"/api/meals/{mealId}");
        var aDay = await a.Client.GetFromJsonAsync<DayMealsResponse>($"/api/meals/{UserApi.Today}", TestJson.Options);

        Assert.Empty(bDay!.Items);
        Assert.Equal(HttpStatusCode.NotFound, bDelete.StatusCode);
        Assert.Single(aDay!.Items);
    }

    [Fact]
    public async Task Water_of_another_user_cannot_be_read_or_deleted()
    {
        var a = await factory.SignUpAsync();
        var b = await factory.SignUpAsync();
        var waterId = await UserApi.IdOfAsync(await UserApi.AddWaterAsync(a.Client, UserApi.Today, 500));

        var bDay = await b.Client.GetFromJsonAsync<WaterDayResponse>($"/api/water/{UserApi.Today}", TestJson.Options);
        var bDelete = await b.Client.DeleteAsync($"/api/water/{waterId}");
        var aDay = await a.Client.GetFromJsonAsync<WaterDayResponse>($"/api/water/{UserApi.Today}", TestJson.Options);

        Assert.Equal(0, bDay!.TotalMl);
        Assert.Equal(HttpStatusCode.NotFound, bDelete.StatusCode);
        Assert.Equal(500, aDay!.TotalMl);
    }

    [Fact]
    public async Task Custom_foods_of_another_user_are_invisible_undeletable_and_unusable()
    {
        var a = await factory.SignUpAsync();
        var b = await factory.SignUpAsync();
        var secretName = "طعام-خاص-" + Guid.NewGuid().ToString("N")[..6];
        var food = await (await a.Client.PostAsJsonAsync("/api/customfoods", new { name = secretName, kcalPer100g = 100, proteinPer100g = 10 }))
            .Content.ReadFromJsonAsync<FoodDto>(TestJson.Options);

        var bSearch = await b.Client.GetFromJsonAsync<List<FoodDto>>("/api/foods?search=" + Uri.EscapeDataString(secretName), TestJson.Options);
        var bDelete = await b.Client.DeleteAsync($"/api/customfoods/{food!.Id}");
        var bMeal = await b.Client.PostAsJsonAsync("/api/meals", new
        {
            date = UserApi.Today, mealType = "snack", source = "custom", customFoodId = food.Id, grams = 100,
        });
        var aSearch = await a.Client.GetFromJsonAsync<List<FoodDto>>("/api/foods?search=" + Uri.EscapeDataString(secretName), TestJson.Options);

        Assert.Empty(bSearch!);
        Assert.Equal(HttpStatusCode.NotFound, bDelete.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bMeal.StatusCode);
        Assert.Single(aSearch!);
    }

    [Fact]
    public async Task Weight_and_profile_are_private()
    {
        var a = await factory.SignUpAsync();
        var b = await factory.SignUpAsync();
        await UserApi.PutProfileAsync(a.Client, weightKg: 95);
        await a.Client.PostAsJsonAsync("/api/weight", new { date = UserApi.DaysAgo(1), weightKg = 96 });

        var bWeight = await b.Client.GetFromJsonAsync<WeightHistoryResponse>("/api/weight", TestJson.Options);
        var bProfile = await b.Client.GetAsync("/api/profile");

        Assert.Empty(bWeight!.Entries);
        Assert.Null(bWeight.TargetWeightKg);
        Assert.Equal(HttpStatusCode.NotFound, bProfile.StatusCode);
    }

    [Fact]
    public async Task Water_reminder_is_per_user()
    {
        var a = await factory.SignUpAsync();
        var b = await factory.SignUpAsync();
        await a.Client.PutAsJsonAsync("/api/water/reminder", new { enabled = true, intervalHours = 1 });

        var bReminder = await b.Client.GetFromJsonAsync<WaterReminderDto>("/api/water/reminder", TestJson.Options);

        Assert.False(bReminder!.Enabled);
    }

    [Fact]
    public async Task History_only_includes_the_callers_own_data()
    {
        var a = await factory.SignUpAsync();
        var b = await factory.SignUpAsync();
        await UserApi.AddAiMealAsync(a.Client, UserApi.Today, 900);
        await UserApi.AddWaterAsync(a.Client, UserApi.Today, 1000);

        var bDays = await b.Client.GetFromJsonAsync<List<HistoryDayDto>>("/api/history/days", TestJson.Options);
        var bSummary = await b.Client.GetFromJsonAsync<HistorySummaryResponse>($"/api/history/summary?days=7&today={UserApi.Today}", TestJson.Options);

        Assert.Empty(bDays!);
        Assert.Equal(0, bSummary!.Streak);
        Assert.All(bSummary.Daily, d => Assert.Equal(0, d.MealCount));
    }
}
