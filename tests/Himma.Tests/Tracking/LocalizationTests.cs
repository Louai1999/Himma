using System.Net;
using System.Net.Http.Json;
using Himma.Api.Data;
using Himma.Api.Features.Foods;
using Himma.Api.Features.Tips;
using Himma.Api.Features.Workouts;
using Himma.Api.Infrastructure.Database;
using Himma.Tests.Auth;
using Himma.Tests.Workouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Himma.Tests.Tracking;

/// <summary>The API answers in Arabic by default and in English when the request asks for it.</summary>
public sealed class LocalizationTests(HimmaApiFactory factory) : IClassFixture<HimmaApiFactory>
{
    private static void UseLanguage(HttpClient client, string language)
    {
        client.DefaultRequestHeaders.AcceptLanguage.Clear();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
    }

    [Theory]
    [InlineData(null, "تعذّر تسجيل الدخول")]
    [InlineData("ar", "تعذّر تسجيل الدخول")]
    [InlineData("en", "Could not sign in")]
    [InlineData("en-US", "Could not sign in")]
    [InlineData("fr", "تعذّر تسجيل الدخول")] // unsupported: falls back to Arabic
    public async Task Error_messages_follow_the_requested_language(string? language, string expectedTitle)
    {
        var client = factory.CreateHttpsClient();
        if (language is not null) UseLanguage(client, language);

        var response = await AuthApi.LoginAsync(client, AuthApi.NewUsername(), AuthApi.NewPassword());

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(expectedTitle, problem!.Title);
    }

    [Fact]
    public async Task Validation_messages_are_translated()
    {
        var user = await factory.SignUpAsync();
        UseLanguage(user.Client, "en");

        var response = await user.Client.PutAsJsonAsync("/api/profile", new { date = UserApi.Today, age = 5 });

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal("Some details need fixing", problem!.Title);
        Assert.Equal("Age must be between 13 and 100.", Assert.Single(problem.Errors["age"]));
        Assert.Equal("Choose your sex.", Assert.Single(problem.Errors["gender"]));
    }

    [Fact]
    public async Task Foods_have_english_names_and_can_be_searched_in_both_languages()
    {
        var user = await factory.SignUpAsync();
        UseLanguage(user.Client, "en");

        var byEnglish = await user.Client.GetFromJsonAsync<List<FoodDto>>("/api/foods?search=CHICKEN", TestJson.Options);
        var byArabic = await user.Client.GetFromJsonAsync<List<FoodDto>>("/api/foods?search=" + Uri.EscapeDataString("كبسة"), TestJson.Options);

        Assert.Contains(byEnglish!, f => f.Name == "Grilled chicken breast");
        Assert.All(byEnglish!, f => Assert.Contains("chicken", f.Name, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Chicken kabsa", Assert.Single(byArabic!).Name);
        Assert.Equal("plate", byArabic![0].ServingLabel);
    }

    [Fact]
    public async Task Exercises_templates_and_tips_come_in_english()
    {
        var user = await factory.SignUpAsync();
        UseLanguage(user.Client, "en");

        var exercises = await WorkoutApi.ExercisesAsync(user.Client, "?search=bench");
        var templates = await user.Client.GetFromJsonAsync<List<TemplateDto>>("/api/plans/templates", TestJson.Options);
        var tip = await user.Client.GetFromJsonAsync<TipDto>("/api/tips/today?date=2026-01-01", TestJson.Options);

        Assert.Contains(exercises, e => e.Name == "Flat barbell bench press" && e.Equipment == "Barbell and bench");
        Assert.Contains(templates!, t => t.Name == "Gym split - 4 days");
        Assert.Equal(DailyTips.AllEnglish[0], tip!.Text);
    }

    [Fact]
    public async Task Cloning_a_system_exercise_in_english_gives_an_english_copy()
    {
        var user = await factory.SignUpAsync();
        UseLanguage(user.Client, "en");
        var squat = (await WorkoutApi.ExercisesAsync(user.Client, "?search=barbell%20squat")).Single(e => e.IsSystem);

        var copy = await (await user.Client.PostAsync($"/api/exercises/{squat.Id}/clone", null)).ReadAsync<ExerciseDto>();

        Assert.Equal("Barbell squat", copy.Name);
        UseLanguage(user.Client, "ar");
        var mine = await WorkoutApi.ExercisesAsync(user.Client, "?mine=true");
        Assert.Equal("Barbell squat", mine.Single(e => e.Id == copy.Id).Name); // the user's copy keeps its text
    }

    [Fact]
    public async Task Startup_seeding_adds_english_names_to_older_databases()
    {
        factory.CreateClient();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Foods.ExecuteUpdateAsync(s => s.SetProperty(f => f.NameEn, (string?)null));
            await db.Exercises.ExecuteUpdateAsync(s => s.SetProperty(e => e.NameEn, (string?)null));
        }

        await factory.Services.InitializeDatabaseAsync();

        await using var check = factory.Services.CreateAsyncScope();
        var checkDb = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await checkDb.Foods.CountAsync(f => f.NameEn == null));
        Assert.Equal(0, await checkDb.Exercises.CountAsync(e => e.OwnerUserId == null && e.NameEn == null));
    }
}
