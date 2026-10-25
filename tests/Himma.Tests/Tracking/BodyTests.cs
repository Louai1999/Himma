using System.Net;
using System.Net.Http.Json;
using Himma.Api.Domain.Body;
using Himma.Api.Features.Body;
using Himma.Tests.Auth;

namespace Himma.Tests.Tracking;

public sealed class BodyTests(HimmaApiFactory factory) : IClassFixture<HimmaApiFactory>
{
    private static Task<HttpResponseMessage> SaveAsync(HttpClient client, object body) =>
        client.PostAsJsonAsync("/api/body", body);

    private static async Task<BodyResponse> GetAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<BodyResponse>("/api/body", TestJson.Options))!;

    [Fact]
    public async Task Tape_measurements_give_body_fat_using_the_weight_log()
    {
        var user = await factory.SignUpAsync();
        await UserApi.PutProfileAsync(user.Client, weightKg: 85);   // male, 180 cm

        (await SaveAsync(user.Client, new { date = UserApi.Today, neckCm = 38, waistCm = 90, armCm = 38 })).EnsureSuccessStatusCode();

        var body = await GetAsync(user.Client);
        var entry = Assert.Single(body.Entries);
        Assert.True(body.HasProfile);
        Assert.Equal(85, entry.WeightKg);
        Assert.Equal(38, entry.ArmCm);
        Assert.Equal(BodyFatSource.Navy, entry.Composition!.Source);
        Assert.InRange(entry.Composition.BodyFatPercent, 19, 21);
        Assert.Equal(entry.Id, body.Latest!.Id);
    }

    [Fact]
    public async Task Each_measurement_uses_the_weight_logged_on_or_before_its_date()
    {
        var user = await factory.SignUpAsync();
        await UserApi.PutProfileAsync(user.Client, weightKg: 90, date: UserApi.DaysAgo(10));
        await user.Client.PostAsJsonAsync("/api/weight", new { date = UserApi.DaysAgo(2), weightKg = 86 });

        await SaveAsync(user.Client, new { date = UserApi.DaysAgo(5), bodyFatPercent = 20 });
        await SaveAsync(user.Client, new { date = UserApi.Today, bodyFatPercent = 18 });

        var body = await GetAsync(user.Client);
        Assert.Equal([90.0, 86.0], body.Entries.Select(e => e.WeightKg!.Value));
        Assert.Equal(BodyFatSource.Device, body.Latest!.Composition!.Source);
        Assert.Equal(70.5, body.Latest.Composition.LeanMassKg);    // 86 * 82%
    }

    [Fact]
    public async Task Saving_the_same_date_again_replaces_the_entry()
    {
        var user = await factory.SignUpAsync();
        await UserApi.PutProfileAsync(user.Client);

        await SaveAsync(user.Client, new { date = UserApi.Today, waistCm = 95 });
        await SaveAsync(user.Client, new { date = UserApi.Today, waistCm = 92, neckCm = 39 });

        var entry = Assert.Single((await GetAsync(user.Client)).Entries);
        Assert.Equal(92, entry.WaistCm);
        Assert.Equal(39, entry.NeckCm);
    }

    [Fact]
    public async Task Body_fat_goal_gives_a_projection_and_can_be_cleared()
    {
        var user = await factory.SignUpAsync();
        await UserApi.PutProfileAsync(user.Client, weightKg: 80);
        await SaveAsync(user.Client, new { date = UserApi.Today, bodyFatPercent = 20 });

        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.PutAsJsonAsync("/api/body/goal", new { targetBodyFatPercent = 12 })).StatusCode);
        var withGoal = await GetAsync(user.Client);
        Assert.Equal(12, withGoal.TargetBodyFatPercent);
        Assert.Equal(72.7, withGoal.Projection!.TargetWeightKg);   // 64 / 0.88
        Assert.Equal(7.3, withGoal.Projection.FatToLoseKg);
        Assert.False(withGoal.Projection.Reached);

        await user.Client.PutAsJsonAsync("/api/body/goal", new { targetBodyFatPercent = (double?)null });
        var cleared = await GetAsync(user.Client);
        Assert.Null(cleared.TargetBodyFatPercent);
        Assert.Null(cleared.Projection);
    }

    [Fact]
    public async Task Without_a_profile_entries_are_kept_but_not_calculated()
    {
        var user = await factory.SignUpAsync();

        (await SaveAsync(user.Client, new { date = UserApi.Today, waistCm = 90, neckCm = 38 })).EnsureSuccessStatusCode();
        var goal = await user.Client.PutAsJsonAsync("/api/body/goal", new { targetBodyFatPercent = 15 });

        var body = await GetAsync(user.Client);
        Assert.False(body.HasProfile);
        Assert.Null(Assert.Single(body.Entries).Composition);
        Assert.Null(body.Latest);
        Assert.Equal(HttpStatusCode.NotFound, goal.StatusCode);
    }

    [Theory]
    [InlineData("waistCm", 10)]
    [InlineData("neckCm", 500)]
    [InlineData("bodyFatPercent", 90)]
    [InlineData("muscleMassKg", 1)]
    [InlineData("date", "2026/10/25")]
    [InlineData("heightCm", 180)]      // unknown field
    public async Task Invalid_measurements_are_rejected(string field, object value)
    {
        var user = await factory.SignUpAsync();
        var body = new Dictionary<string, object?> { ["date"] = UserApi.Today, ["armCm"] = 35 };
        body[field] = value;

        Assert.Equal(HttpStatusCode.BadRequest, (await SaveAsync(user.Client, body)).StatusCode);
    }

    [Fact]
    public async Task An_entry_needs_at_least_one_measurement()
    {
        var user = await factory.SignUpAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await SaveAsync(user.Client, new { date = UserApi.Today })).StatusCode);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(51)]
    public async Task Goal_outside_the_allowed_range_is_rejected(double target)
    {
        var user = await factory.SignUpAsync();
        await UserApi.PutProfileAsync(user.Client);

        var response = await user.Client.PutAsJsonAsync("/api/body/goal", new { targetBodyFatPercent = target });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Measurements_of_another_user_cannot_be_read_or_deleted()
    {
        var a = await factory.SignUpAsync();
        var b = await factory.SignUpAsync();
        var id = await UserApi.IdOfAsync(await SaveAsync(a.Client, new { date = UserApi.Today, waistCm = 90 }));

        var bView = await GetAsync(b.Client);
        var bDelete = await b.Client.DeleteAsync($"/api/body/{id}");

        Assert.Empty(bView.Entries);
        Assert.Equal(HttpStatusCode.NotFound, bDelete.StatusCode);
        Assert.Single((await GetAsync(a.Client)).Entries);

        Assert.Equal(HttpStatusCode.NoContent, (await a.Client.DeleteAsync($"/api/body/{id}")).StatusCode);
        Assert.Empty((await GetAsync(a.Client)).Entries);
    }

    [Fact]
    public async Task Body_endpoints_need_a_signed_in_user()
    {
        var anonymous = factory.CreateHttpsClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/body")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/body", new { date = UserApi.Today, waistCm = 90 })).StatusCode);
    }
}
