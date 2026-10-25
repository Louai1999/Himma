using System.Net;
using System.Net.Http.Json;
using Himma.Api.Domain;
using Himma.Api.Domain.Strength;
using Himma.Api.Domain.Walking;
using Himma.Api.Features.Steps;
using Himma.Api.Features.Workouts;
using Himma.Tests.Auth;
using Himma.Tests.Tracking;

namespace Himma.Tests.Workouts;

public sealed class OneRepMaxTests
{
    [Fact]
    public void Epley_estimates_the_max_from_weight_and_reps()
    {
        Assert.Equal(116.7, OneRepMax.Estimate(100, 5));   // 100 × (1 + 5/30)
        Assert.Equal(100, OneRepMax.Estimate(100, 1));
    }

    [Fact]
    public void The_load_chart_uses_gym_steps_of_half_a_kilo()
    {
        var estimate = OneRepMax.Calculate(100, 5);

        Assert.True(estimate.Reliable);
        Assert.Equal(new LoadRow(100, 116.5, 1), estimate.Table[0]);   // 116.7 rounded to 0.5
        Assert.Equal(new LoadRow(80, 93.5, 8), estimate.Table.Single(r => r.Percent == 80));
    }

    [Fact]
    public void High_rep_estimates_are_flagged_as_less_reliable()
    {
        Assert.False(OneRepMax.Calculate(40, 15).Reliable);
    }
}

public sealed class WalkingCalculatorTests
{
    [Fact]
    public void Steps_become_distance_and_calories()
    {
        // Step 180 × 0.415 = 74.7 cm; 10,000 steps = 7.47 km; 0.7 × 80 × 7.47 = 418 kcal
        Assert.Equal(0.747, WalkingCalculator.StepLengthM(Gender.Male, 180), 3);
        Assert.Equal(7.5, WalkingCalculator.DistanceKm(10_000, WalkingCalculator.StepLengthM(Gender.Male, 180)));
        Assert.Equal(418, WalkingCalculator.CaloriesForSteps(Gender.Male, 180, 80, 10_000));
    }

    [Fact]
    public void Plan_says_how_far_how_many_steps_and_how_long()
    {
        // 300 / (0.7 × 80) = 5.36 km = 7,172 steps of 0.747 m = 64 minutes at 5 km/h
        var plan = WalkingCalculator.Plan(Gender.Male, 180, 80, 300);

        Assert.Equal(new WalkingPlan(300, 5.4, 7172, 64), plan);
    }
}

public sealed class StrengthAndStepsTests(HimmaApiFactory factory) : IClassFixture<HimmaApiFactory>
{
    // ---------- Steps ----------

    [Fact]
    public async Task Steps_are_saved_per_day_with_distance_and_calories()
    {
        var user = await factory.SignUpAsync();
        await UserApi.PutProfileAsync(user.Client, weightKg: 80);   // male, 180 cm

        await user.Client.PostAsJsonAsync("/api/steps", new { date = UserApi.Today, steps = 6000 });
        await user.Client.PostAsJsonAsync("/api/steps", new { date = UserApi.Today, steps = 10_000 });   // replaces
        await user.Client.PostAsJsonAsync("/api/steps", new { date = UserApi.DaysAgo(1), steps = 8000 });

        var steps = await user.Client.GetFromJsonAsync<StepsResponse>($"/api/steps?today={UserApi.Today}", TestJson.Options);
        Assert.True(steps!.HasProfile);
        Assert.Equal(2, steps.Entries.Count);
        var today = steps.Entries[^1];
        Assert.Equal(10_000, today.Steps);
        Assert.Equal(7.5, today.DistanceKm);
        Assert.Equal(418, today.Calories);
        Assert.Equal(9000, steps.WeekAverage);
    }

    [Fact]
    public async Task Without_a_profile_steps_are_kept_but_not_converted()
    {
        var user = await factory.SignUpAsync();

        await user.Client.PostAsJsonAsync("/api/steps", new { date = UserApi.Today, steps = 5000 });

        var steps = await user.Client.GetFromJsonAsync<StepsResponse>("/api/steps", TestJson.Options);
        Assert.False(steps!.HasProfile);
        Assert.Null(Assert.Single(steps.Entries).DistanceKm);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.GetAsync("/api/steps/plan?calories=300")).StatusCode);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100_001)]
    public async Task Impossible_step_counts_are_rejected(int steps)
    {
        var user = await factory.SignUpAsync();

        var response = await user.Client.PostAsJsonAsync("/api/steps", new { date = UserApi.Today, steps });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_walking_plan_uses_the_profile()
    {
        var user = await factory.SignUpAsync();
        await UserApi.PutProfileAsync(user.Client, weightKg: 80);

        var plan = await user.Client.GetFromJsonAsync<WalkingPlan>("/api/steps/plan?calories=300", TestJson.Options);

        Assert.Equal(7172, plan!.Steps);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.Client.GetAsync("/api/steps/plan?calories=5")).StatusCode);
    }

    [Fact]
    public async Task Steps_of_another_user_cannot_be_seen_or_deleted()
    {
        var a = await factory.SignUpAsync();
        var b = await factory.SignUpAsync();
        await a.Client.PostAsJsonAsync("/api/steps", new { date = UserApi.Today, steps = 7000 });
        var id = (await a.Client.GetFromJsonAsync<StepsResponse>("/api/steps", TestJson.Options))!.Entries.Single().Id;

        Assert.Empty((await b.Client.GetFromJsonAsync<StepsResponse>("/api/steps", TestJson.Options))!.Entries);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Client.DeleteAsync($"/api/steps/{id}")).StatusCode);
        Assert.Single((await a.Client.GetFromJsonAsync<StepsResponse>("/api/steps", TestJson.Options))!.Entries);
    }

    // ---------- 1RM and records ----------

    [Fact]
    public async Task One_rep_max_endpoint_estimates_and_validates()
    {
        var user = await factory.SignUpAsync();

        var estimate = await user.Client.GetFromJsonAsync<OneRepMaxEstimate>("/api/strength/one-rep-max?weightKg=100&reps=5", TestJson.Options);

        Assert.Equal(116.7, estimate!.OneRepMaxKg);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.Client.GetAsync("/api/strength/one-rep-max?weightKg=100&reps=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.Client.GetAsync("/api/strength/one-rep-max?weightKg=900&reps=5")).StatusCode);
    }

    private static async Task<int> SessionAsync(SignedInUser user, string date, params (int ExerciseId, int SetNumber, int Reps, double? WeightKg, bool Completed)[] sets)
    {
        var session = await (await WorkoutApi.StartSessionAsync(user.Client, date, null)).ReadAsync<SessionDto>();
        (await WorkoutApi.SaveSetsAsync(user.Client, session.Id, sets)).EnsureSuccessStatusCode();
        return session.Id;
    }

    [Fact]
    public async Task Beating_earlier_sessions_is_a_new_record_and_the_first_time_is_not()
    {
        var user = await factory.SignUpAsync();
        var bench = await (await WorkoutApi.CreateExerciseAsync(user.Client, "ضغط بار")).ReadAsync<ExerciseDto>();

        var first = await SessionAsync(user, UserApi.DaysAgo(7), (bench.Id, 1, 8, 60, true), (bench.Id, 2, 6, 62.5, true));
        var second = await SessionAsync(user, UserApi.Today, (bench.Id, 1, 8, 65, true), (bench.Id, 2, 12, 40, false));

        var firstRecords = await user.Client.GetFromJsonAsync<List<NewRecordDto>>($"/api/workouts/sessions/{first}/records", TestJson.Options);
        var secondRecords = await user.Client.GetFromJsonAsync<List<NewRecordDto>>($"/api/workouts/sessions/{second}/records", TestJson.Options);

        Assert.Empty(firstRecords!);
        Assert.Equal(2, secondRecords!.Count);
        var weight = secondRecords.Single(r => r.Kind == RecordKinds.Weight);
        Assert.Equal(65, weight.ValueKg);
        Assert.Equal(62.5, weight.PreviousKg);
        var max = secondRecords.Single(r => r.Kind == RecordKinds.OneRepMax);
        Assert.Equal(82.3, max.ValueKg);   // 65 × (1 + 8/30); the unticked 40 × 12 set does not count
    }

    [Fact]
    public async Task Records_list_the_best_set_and_the_heaviest_weight_per_exercise()
    {
        var user = await factory.SignUpAsync();
        var squat = await (await WorkoutApi.CreateExerciseAsync(user.Client, "سكوات")).ReadAsync<ExerciseDto>();
        await SessionAsync(user, UserApi.DaysAgo(3), (squat.Id, 1, 10, 80, true));    // 1RM 106.7
        await SessionAsync(user, UserApi.Today, (squat.Id, 1, 2, 95, true));          // 1RM 101.3, but heavier

        var records = await user.Client.GetFromJsonAsync<List<PersonalRecordDto>>("/api/workouts/records", TestJson.Options);

        var record = Assert.Single(records!);
        Assert.Equal("سكوات", record.ExerciseName);
        Assert.Equal(106.7, record.BestOneRepMaxKg);
        Assert.Equal(80, record.BestSetWeightKg);
        Assert.Equal(10, record.BestSetReps);
        Assert.Equal(95, record.HeaviestWeightKg);
        Assert.Equal(2, record.Sessions);
    }

    [Fact]
    public async Task Records_of_another_users_session_are_not_found()
    {
        var a = await factory.SignUpAsync();
        var b = await factory.SignUpAsync();
        var bench = await (await WorkoutApi.CreateExerciseAsync(a.Client)).ReadAsync<ExerciseDto>();
        var session = await SessionAsync(a, UserApi.Today, (bench.Id, 1, 5, 50, true));

        Assert.Equal(HttpStatusCode.NotFound, (await b.Client.GetAsync($"/api/workouts/sessions/{session}/records")).StatusCode);
        Assert.Empty((await b.Client.GetFromJsonAsync<List<PersonalRecordDto>>("/api/workouts/records", TestJson.Options))!);
    }
}
