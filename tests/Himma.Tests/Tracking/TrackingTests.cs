using System.Net;
using System.Net.Http.Json;
using Himma.Api.Domain.Nutrition;
using Himma.Api.Features.Foods;
using Himma.Api.Features.History;
using Himma.Api.Features.Meals;
using Himma.Api.Features.Profile;
using Himma.Api.Features.Water;
using Himma.Api.Features.Weight;
using Himma.Tests.Auth;

namespace Himma.Tests.Tracking;

public sealed class TrackingTests(HimmaApiFactory factory) : IClassFixture<HimmaApiFactory>
{
    // ---------- Profile ----------

    [Fact]
    public async Task Profile_is_404_until_saved_then_returns_targets()
    {
        var user = await factory.SignUpAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.GetAsync("/api/profile")).StatusCode);

        var saved = await UserApi.PutProfileAsync(user.Client);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var body = await saved.Content.ReadFromJsonAsync<ProfileResponse>(TestJson.Options);
        Assert.Equal(2259, body!.Targets.TargetCalories);
        Assert.Equal(160, body.Targets.ProteinTargetG);
        Assert.Equal(3150, body.Targets.WaterTargetMl);

        var fetched = await user.Client.GetFromJsonAsync<ProfileResponse>("/api/profile", TestJson.Options);
        Assert.Equal(body.Targets.TargetCalories, fetched!.Targets.TargetCalories);
    }

    [Fact]
    public async Task First_profile_save_seeds_one_weight_entry()
    {
        var user = await factory.SignUpAsync();

        await UserApi.PutProfileAsync(user.Client, weightKg: 80);
        await UserApi.PutProfileAsync(user.Client, weightKg: 81);

        var weight = await user.Client.GetFromJsonAsync<WeightHistoryResponse>("/api/weight", TestJson.Options);
        var entry = Assert.Single(weight!.Entries);
        Assert.Equal(80, entry.WeightKg);
        Assert.Equal(72, weight.TargetWeightKg);
    }

    [Theory]
    [InlineData("gender", "alien")]   // unknown enum name
    [InlineData("gender", 0)]         // enum as number
    [InlineData("age", 5)]            // out of range
    [InlineData("heightCm", 20)]
    [InlineData("date", "22-10-2026")] // wrong date format
    public async Task Profile_rejects_invalid_values(string field, object value)
    {
        var user = await factory.SignUpAsync();
        var body = new Dictionary<string, object?>
        {
            ["date"] = UserApi.Today,
            ["gender"] = "female",
            ["age"] = 28,
            ["heightCm"] = 165,
            ["weightKg"] = 60,
            ["activityLevel"] = "light",
            ["goal"] = "maintain",
        };
        body[field] = value;

        var response = await user.Client.PutAsJsonAsync("/api/profile", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Profile_rejects_missing_required_fields()
    {
        var user = await factory.SignUpAsync();

        var response = await user.Client.PutAsJsonAsync("/api/profile", new { date = UserApi.Today, age = 30 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Foods and meals ----------

    [Fact]
    public async Task Foods_search_finds_seeded_arabic_foods()
    {
        var user = await factory.SignUpAsync();

        var foods = await user.Client.GetFromJsonAsync<List<FoodDto>>("/api/foods?search=" + Uri.EscapeDataString("دجاج"), TestJson.Options);

        Assert.NotEmpty(foods!);
        Assert.All(foods!, f => Assert.Contains("دجاج", f.Name));
    }

    [Fact]
    public async Task Meal_from_food_database_is_calculated_by_the_server()
    {
        var user = await factory.SignUpAsync();
        var chicken = (await user.Client.GetFromJsonAsync<List<FoodDto>>("/api/foods?search=" + Uri.EscapeDataString("صدر دجاج مشوي"), TestJson.Options))!.Single();

        var created = await user.Client.PostAsJsonAsync("/api/meals", new
        {
            date = UserApi.Today,
            mealType = "dinner",
            source = "manual",
            foodId = chicken.Id,
            grams = 200,
            calories = 1, // ignored: the server calculates from the food
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var meal = await created.Content.ReadFromJsonAsync<MealDto>(TestJson.Options);
        Assert.Equal(330, meal!.Calories);  // 165 kcal/100 g * 2
        Assert.Equal(62, meal.ProteinG);    // 31 g/100 g * 2

        var day = await user.Client.GetFromJsonAsync<DayMealsResponse>($"/api/meals/{UserApi.Today}", TestJson.Options);
        Assert.Equal(330, day!.Totals.Calories);
    }

    [Fact]
    public async Task Meal_from_custom_food_uses_the_users_values()
    {
        var user = await factory.SignUpAsync();
        var food = await (await user.Client.PostAsJsonAsync("/api/customfoods", new { name = "خبز أمي", kcalPer100g = 250, proteinPer100g = 8 }))
            .Content.ReadFromJsonAsync<FoodDto>(TestJson.Options);

        var created = await user.Client.PostAsJsonAsync("/api/meals", new
        {
            date = UserApi.Today, mealType = "breakfast", source = "custom", customFoodId = food!.Id, grams = 50,
        });

        var meal = await created.Content.ReadFromJsonAsync<MealDto>(TestJson.Options);
        Assert.Equal(125, meal!.Calories);
        Assert.Equal(4, meal.ProteinG);
        Assert.Equal("custom", (await user.Client.GetFromJsonAsync<List<FoodDto>>("/api/foods?search=" + Uri.EscapeDataString("خبز أمي"), TestJson.Options))!.First().Source);
    }

    [Theory]
    [InlineData(6000)]   // absurd for one meal
    [InlineData(-5)]
    public async Task Ai_meal_with_unrealistic_calories_is_rejected(double calories)
    {
        var user = await factory.SignUpAsync();

        var response = await UserApi.AddAiMealAsync(user.Client, UserApi.Today, calories);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Meals_cannot_be_logged_in_the_future_or_with_a_bad_date()
    {
        var user = await factory.SignUpAsync();
        var future = DateTime.UtcNow.AddDays(5).ToString("yyyy-MM-dd");

        Assert.Equal(HttpStatusCode.BadRequest, (await UserApi.AddAiMealAsync(user.Client, future)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await UserApi.AddAiMealAsync(user.Client, "2026-13-40")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.Client.GetAsync("/api/meals/not-a-date")).StatusCode);
    }

    [Fact]
    public async Task Meal_can_be_deleted()
    {
        var user = await factory.SignUpAsync();
        var id = await UserApi.IdOfAsync(await UserApi.AddAiMealAsync(user.Client, UserApi.Today));

        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.DeleteAsync($"/api/meals/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.DeleteAsync($"/api/meals/{id}")).StatusCode);
    }

    // ---------- Water ----------

    [Fact]
    public async Task Water_is_summed_per_day_and_can_be_removed()
    {
        var user = await factory.SignUpAsync();
        await UserApi.AddWaterAsync(user.Client, UserApi.Today, 250);
        var id = await UserApi.IdOfAsync(await UserApi.AddWaterAsync(user.Client, UserApi.Today, 500));
        await UserApi.AddWaterAsync(user.Client, UserApi.DaysAgo(1), 1000);

        var day = await user.Client.GetFromJsonAsync<WaterDayResponse>($"/api/water/{UserApi.Today}", TestJson.Options);
        Assert.Equal(750, day!.TotalMl);

        await user.Client.DeleteAsync($"/api/water/{id}");
        day = await user.Client.GetFromJsonAsync<WaterDayResponse>($"/api/water/{UserApi.Today}", TestJson.Options);
        Assert.Equal(250, day!.TotalMl);
    }

    [Fact]
    public async Task Water_reminder_defaults_and_can_be_changed()
    {
        var user = await factory.SignUpAsync();

        var initial = await user.Client.GetFromJsonAsync<WaterReminderDto>("/api/water/reminder", TestJson.Options);
        Assert.False(initial!.Enabled);

        await user.Client.PutAsJsonAsync("/api/water/reminder", new { enabled = true, intervalHours = 3 });
        var updated = await user.Client.GetFromJsonAsync<WaterReminderDto>("/api/water/reminder", TestJson.Options);
        Assert.True(updated!.Enabled);
        Assert.Equal(3, updated.IntervalHours);

        var invalid = await user.Client.PutAsJsonAsync("/api/water/reminder", new { enabled = true, intervalHours = 24 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    // ---------- Weight ----------

    [Fact]
    public async Task Weight_upserts_by_date_and_updates_profile_targets()
    {
        var user = await factory.SignUpAsync();
        await UserApi.PutProfileAsync(user.Client, weightKg: 80, date: UserApi.DaysAgo(3));

        await user.Client.PostAsJsonAsync("/api/weight", new { date = UserApi.Today, weightKg = 79 });
        await user.Client.PostAsJsonAsync("/api/weight", new { date = UserApi.Today, weightKg = 78.5 });

        var weight = await user.Client.GetFromJsonAsync<WeightHistoryResponse>("/api/weight", TestJson.Options);
        Assert.Equal(2, weight!.Entries.Count);
        Assert.Equal(80, weight.StartWeightKg);
        Assert.Equal(78.5, weight.LatestWeightKg);

        var profile = await user.Client.GetFromJsonAsync<ProfileResponse>("/api/profile", TestJson.Options);
        Assert.Equal(78.5, profile!.Profile.WeightKg);
        Assert.Equal(157, profile.Targets.ProteinTargetG); // 78.5 * 2.0
    }

    [Fact]
    public async Task Backfilling_an_old_weight_does_not_change_the_current_profile_weight()
    {
        var user = await factory.SignUpAsync();
        await UserApi.PutProfileAsync(user.Client, weightKg: 80);

        await user.Client.PostAsJsonAsync("/api/weight", new { date = UserApi.DaysAgo(10), weightKg = 85 });

        var profile = await user.Client.GetFromJsonAsync<ProfileResponse>("/api/profile", TestJson.Options);
        Assert.Equal(80, profile!.Profile.WeightKg);
    }

    // ---------- History ----------

    [Fact]
    public async Task History_days_returns_totals_per_day_with_data()
    {
        var user = await factory.SignUpAsync();
        await UserApi.AddAiMealAsync(user.Client, UserApi.Today, 400, 20);
        await UserApi.AddAiMealAsync(user.Client, UserApi.Today, 600, 40);
        await UserApi.AddWaterAsync(user.Client, UserApi.DaysAgo(2), 750);

        var days = await user.Client.GetFromJsonAsync<List<HistoryDayDto>>("/api/history/days", TestJson.Options);

        Assert.Equal(2, days!.Count);
        var today = days.Single(d => d.Date.ToString("yyyy-MM-dd") == UserApi.Today);
        Assert.Equal(1000, today.Calories);
        Assert.Equal(60, today.ProteinG);
        Assert.Equal(2, today.MealCount);
        Assert.Equal(750, days.Single(d => d.Date.ToString("yyyy-MM-dd") == UserApi.DaysAgo(2)).WaterMl);
    }

    [Fact]
    public async Task History_summary_reports_streak_adherence_and_one_bar_per_day()
    {
        var user = await factory.SignUpAsync();
        await UserApi.PutProfileAsync(user.Client); // target 2259 kcal, 160 g protein, 3150 ml
        await UserApi.AddAiMealAsync(user.Client, UserApi.Today, 2259, 160);
        await UserApi.AddAiMealAsync(user.Client, UserApi.DaysAgo(1), 1000, 50);
        await UserApi.AddWaterAsync(user.Client, UserApi.Today, 2000);
        await UserApi.AddWaterAsync(user.Client, UserApi.Today, 1000);

        var summary = await user.Client.GetFromJsonAsync<HistorySummaryResponse>($"/api/history/summary?days=14&today={UserApi.Today}", TestJson.Options);

        Assert.Equal(14, summary!.Daily.Count);
        Assert.Equal(2, summary.Streak);
        Assert.Equal(2, summary.Adherence.LoggedDays);
        Assert.Equal(7.1, summary.Adherence.CalorieAdherencePercent); // 1 of 14 days
        Assert.True(summary.Daily[^1].CalorieAdherent);
        Assert.True(summary.Daily[^1].WaterAdherent);
        Assert.False(summary.Daily[^2].CalorieAdherent);
        Assert.Single(summary.Weights);
    }

    [Theory]
    [InlineData("/api/history/summary?days=0")]
    [InlineData("/api/history/summary?days=400")]
    [InlineData("/api/history/summary?today=2001-01-01")]
    [InlineData("/api/history/days?from=2026-01-10&to=2026-01-01")]
    public async Task History_rejects_invalid_ranges(string url)
    {
        var user = await factory.SignUpAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await user.Client.GetAsync(url)).StatusCode);
    }

    // ---------- Everything requires sign-in ----------

    [Theory]
    [InlineData("GET", "/api/profile")]
    [InlineData("GET", "/api/foods")]
    [InlineData("POST", "/api/customfoods")]
    [InlineData("GET", "/api/meals/2026-01-01")]
    [InlineData("POST", "/api/meals")]
    [InlineData("GET", "/api/water/2026-01-01")]
    [InlineData("GET", "/api/water/reminder")]
    [InlineData("GET", "/api/weight")]
    [InlineData("GET", "/api/history/days")]
    [InlineData("GET", "/api/history/summary")]
    public async Task Tracking_endpoints_require_authentication(string method, string url)
    {
        var anonymous = factory.CreateHttpsClient();

        var response = await anonymous.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
