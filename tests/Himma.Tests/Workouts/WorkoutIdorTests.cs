using System.Net;
using System.Net.Http.Json;
using Himma.Api.Data.Seed;
using Himma.Api.Domain;
using Himma.Api.Features.Workouts;
using Himma.Tests.Tracking;

namespace Himma.Tests.Workouts;

/// <summary>Only the owner can read, change or use their exercises, plans and sessions.</summary>
public sealed class WorkoutIdorTests(HimmaApiFactory factory) : IClassFixture<HimmaApiFactory>
{
    [Fact]
    public async Task Another_users_exercise_is_invisible_and_cannot_be_changed_cloned_or_used()
    {
        var a = await factory.SignUpAsync();
        var b = await factory.SignUpAsync();
        var secret = await (await WorkoutApi.CreateExerciseAsync(a.Client, "تمرين سري")).ReadAsync<ExerciseDto>();
        var bPlan = await (await WorkoutApi.CreatePlanAsync(b.Client)).ReadAsync<PlanDetailDto>();
        var bSession = await (await WorkoutApi.StartSessionAsync(b.Client, UserApi.Today, null)).ReadAsync<SessionDto>();

        var bList = await WorkoutApi.ExercisesAsync(b.Client);
        var bEdit = await b.Client.PutAsJsonAsync($"/api/exercises/{secret.Id}", new
        {
            name = "مخترق", muscleGroup = "chest", region = "upper", place = "home", defaultSets = 3, defaultReps = 10,
        });
        var bDelete = await b.Client.DeleteAsync($"/api/exercises/{secret.Id}");
        var bClone = await b.Client.PostAsync($"/api/exercises/{secret.Id}/clone", null);
        var bUseInPlan = await WorkoutApi.PutDayAsync(b.Client, bPlan.Id, "sat", WorkoutApi.TrainingDay((secret.Id, 3, 10, null)));
        var bUseInSession = await WorkoutApi.SaveSetsAsync(b.Client, bSession.Id, (secret.Id, 1, 10, null, true));

        Assert.DoesNotContain(bList, e => e.Id == secret.Id);
        Assert.Equal(HttpStatusCode.NotFound, bEdit.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, bDelete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, bClone.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bUseInPlan.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bUseInSession.StatusCode);

        var aMine = await WorkoutApi.ExercisesAsync(a.Client, "?mine=true");
        Assert.Equal("تمرين سري", aMine.Single(e => e.Id == secret.Id).Name);
    }

    [Fact]
    public async Task Another_users_plan_cannot_be_read_changed_activated_or_deleted()
    {
        var a = await factory.SignUpAsync();
        var b = await factory.SignUpAsync();
        var plan = await (await WorkoutApi.ApplyTemplateAsync(a.Client)).ReadAsync<PlanDetailDto>();
        var bench = await WorkoutApi.SystemExerciseAsync(b.Client, ExerciseSeedData.FlatBenchPress);

        var bList = await b.Client.GetFromJsonAsync<List<PlanSummaryDto>>("/api/plans", TestJson.Options);
        var bGet = await b.Client.GetAsync($"/api/plans/{plan.Id}");
        var bRename = await b.Client.PutAsJsonAsync($"/api/plans/{plan.Id}", new { name = "مخترق", place = "gym" });
        var bDay = await WorkoutApi.PutDayAsync(b.Client, plan.Id, "sat", WorkoutApi.TrainingDay((bench.Id, 3, 10, null)));
        var bActivate = await b.Client.PostAsync($"/api/plans/{plan.Id}/activate", null);
        var bDelete = await b.Client.DeleteAsync($"/api/plans/{plan.Id}");

        Assert.Empty(bList!);
        Assert.Equal(HttpStatusCode.NotFound, bGet.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, bRename.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, bDay.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, bActivate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, bDelete.StatusCode);

        var aPlan = await a.Client.GetFromJsonAsync<PlanDetailDto>($"/api/plans/{plan.Id}", TestJson.Options);
        Assert.Equal(plan.Name, aPlan!.Name);
        Assert.True(aPlan.IsActive);
        Assert.Equal(5, aPlan.Days.Single(d => d.DayKey == DayKey.Sat).Items.Count);
    }

    [Fact]
    public async Task Another_users_sessions_cannot_be_started_read_changed_or_listed()
    {
        var a = await factory.SignUpAsync();
        var b = await factory.SignUpAsync();
        var plan = await (await WorkoutApi.ApplyTemplateAsync(a.Client)).ReadAsync<PlanDetailDto>();
        var aDayId = plan.Days.First(d => !d.IsRest).Id;
        var aSession = await (await WorkoutApi.StartSessionAsync(a.Client, UserApi.Today, aDayId)).ReadAsync<SessionDto>();
        var squat = await WorkoutApi.SystemExerciseAsync(b.Client, ExerciseSeedData.Squats);

        var bStartOnADay = await WorkoutApi.StartSessionAsync(b.Client, UserApi.Today, aDayId);
        var bGet = await b.Client.GetAsync($"/api/workouts/sessions/{aSession.Id}");
        var bSets = await WorkoutApi.SaveSetsAsync(b.Client, aSession.Id, (squat.Id, 1, 10, null, true));
        var bComplete = await b.Client.PostAsync($"/api/workouts/sessions/{aSession.Id}/complete", null);
        var bHistory = await b.Client.GetFromJsonAsync<List<WorkoutHistoryItemDto>>("/api/workouts/history", TestJson.Options);

        Assert.Equal(HttpStatusCode.BadRequest, bStartOnADay.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, bGet.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, bSets.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, bComplete.StatusCode);
        Assert.Empty(bHistory!);

        var aView = await a.Client.GetFromJsonAsync<SessionDto>($"/api/workouts/sessions/{aSession.Id}", TestJson.Options);
        Assert.Null(aView!.CompletedAtUtc);
        Assert.Empty(aView.Sets);
    }

    [Fact]
    public async Task Weights_from_another_users_sessions_never_show_up()
    {
        var a = await factory.SignUpAsync();
        var b = await factory.SignUpAsync();
        var aPlan = await (await WorkoutApi.ApplyTemplateAsync(a.Client)).ReadAsync<PlanDetailDto>();
        var aSat = aPlan.Days.Single(d => d.DayKey == DayKey.Sat);
        var bench = aSat.Items[0].Exercise;
        var aSession = await (await WorkoutApi.StartSessionAsync(a.Client, UserApi.Today, aSat.Id)).ReadAsync<SessionDto>();
        await WorkoutApi.SaveSetsAsync(a.Client, aSession.Id, (bench.Id, 1, 5, 140, true));

        var bPlan = await (await WorkoutApi.ApplyTemplateAsync(b.Client)).ReadAsync<PlanDetailDto>();

        Assert.Null(bPlan.Days.Single(d => d.DayKey == DayKey.Sat).Items[0].Last);
    }
}
