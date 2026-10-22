using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Himma.Tests.Auth;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Himma.Tests.Tracking;

/// <summary>A registered user plus an HTTPS client that sends their access token.</summary>
internal sealed record SignedInUser(HttpClient Client, AuthApi.Session Session);

internal static class UserApi
{
    public static async Task<SignedInUser> SignUpAsync(this WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateHttpsClient();
        var session = await AuthApi.RegisterNewUserAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return new SignedInUser(client, session);
    }

    public static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string DaysAgo(int days) => DateTime.UtcNow.AddDays(-days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static Task<HttpResponseMessage> PutProfileAsync(HttpClient client, double weightKg = 80, string? date = null, string goal = "lose") =>
        client.PutAsJsonAsync("/api/profile", new
        {
            date = date ?? Today,
            gender = "male",
            age = 30,
            heightCm = 180,
            weightKg,
            activityLevel = "moderate",
            goal,
            targetWeightKg = 72,
        });

    public static Task<HttpResponseMessage> AddAiMealAsync(HttpClient client, string date, double calories = 500, double protein = 30) =>
        client.PostAsJsonAsync("/api/meals", new
        {
            date,
            mealType = "lunch",
            source = "ai",
            name = "صحن كبسة",
            calories,
            proteinG = protein,
        });

    public static Task<HttpResponseMessage> AddWaterAsync(HttpClient client, string date, int ml = 250) =>
        client.PostAsJsonAsync("/api/water", new { date, ml });

    public static async Task<int> IdOfAsync(HttpResponseMessage created)
    {
        created.EnsureSuccessStatusCode();
        var body = await created.Content.ReadFromJsonAsync<IdOnly>(TestJson.Options);
        return body!.Id;
    }

    private sealed record IdOnly(int Id);
}
