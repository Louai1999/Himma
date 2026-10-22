using System.Net;
using System.Net.Http.Json;
using Himma.Api.Data.Seed;
using Himma.Api.Domain;
using Himma.Api.Features.Workouts;
using Himma.Tests.Auth;
using Himma.Tests.Tracking;

namespace Himma.Tests.Workouts;

public sealed class WorkoutTests(HimmaApiFactory factory) : IClassFixture<HimmaApiFactory>
{
    // ---------- Seeded library and templates ----------

    [Fact]
    public async Task Library_has_enough_system_exercises_for_every_muscle()
    {
        var user = await factory.SignUpAsync();
        var gym = await WorkoutApi.ExercisesAsync(user.Client, "?place=gym");

        int Count(MuscleGroup m) => gym.Count(e => e.IsSystem && e.MuscleGroup == m);
        foreach (var big in new[] { MuscleGroup.Chest, MuscleGroup.Back, MuscleGroup.Legs, MuscleGroup.Shoulders })
        {
            Assert.True(Count(big) >= 5, $"{big} has only {Count(big)} exercises");
        }
        foreach (var small in new[] { MuscleGroup.Triceps, MuscleGroup.Biceps, MuscleGroup.Abs })
        {
            Assert.True(Count(small) >= 3, $"{small} has only {Count(small)} exercises");
        }

        var chestRegions = gym.Where(e => e.MuscleGroup == MuscleGroup.Chest).Select(e => e.Region).ToHashSet();
        Assert.Contains(BodyRegion.Upper, chestRegions);
        Assert.Contains(BodyRegion.Middle, chestRegions);
        Assert.Contains(BodyRegion.Lower, chestRegions);
    }

    [Fact]
    public async Task Home_library_has_the_beginner_exercises()
    {
        var user = await factory.SignUpAsync();
        var home = (await WorkoutApi.ExercisesAsync(user.Client, "?place=home")).Select(e => e.Name).ToHashSet();

        foreach (var name in new[]
        {
            ExerciseSeedData.PushUps, ExerciseSeedData.Squats, ExerciseSeedData.Lunges, ExerciseSeedData.Plank,
            ExerciseSeedData.JumpingJacks, ExerciseSeedData.MountainClimbers, ExerciseSeedData.GluteBridge, ExerciseSeedData.Burpees,
        })
        {
            Assert.Contains(name, home);
        }
    }

    [Theory]
    [InlineData("?muscle=7")]          // numbers are not accepted
    [InlineData("?muscle=wings")]
    [InlineData("?place=home,gym")]
    public async Task Library_filters_reject_unknown_values(string query)
    {
        var user = await factory.SignUpAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await user.Client.GetAsync("/api/exercises" + query)).StatusCode);
    }

    [Fact]
    public async Task Both_templates_are_listed_with_resolved_exercises()
    {
        var user = await factory.SignUpAsync();

        var templates = await user.Client.GetFromJsonAsync<List<TemplateDto>>("/api/plans/templates", TestJson.Options);

        Assert.Equal(2, templates!.Count);
        var gym = templates.Single(t => t.Key == WorkoutTemplates.GymSplit4);
        Assert.Equal(7, gym.Days.Count);
        Assert.Equal(4, gym.Days.Count(d => !d.IsRest));
        Assert.Equal([MuscleGroup.Chest, MuscleGroup.Triceps], gym.Days.Single(d => d.DayKey == DayKey.Sat).Focus);
        Assert.All(gym.Days.Where(d => !d.IsRest), d => Assert.Equal(5, d.Items.Count));
        Assert.Equal(3, templates.Single(t => t.Key == WorkoutTemplates.HomeBeginner3).Days.Count(d => !d.IsRest));
    }

    // ---------- Plans ----------

    [Fact]
    public async Task Applying_a_template_copies_it_into_an_active_plan_that_can_be_edited_freely()
    {
        var user = await factory.SignUpAsync();

        var plan = await (await WorkoutApi.ApplyTemplateAsync(user.Client)).ReadAsync<PlanDetailDto>();
        Assert.True(plan.IsActive);
        Assert.Equal(WorkoutTemplates.GymSplit4, plan.BasedOnTemplateKey);
        Assert.Equal(5, plan.Days.Single(d => d.DayKey == DayKey.Sat).Items.Count);

        // Turning Saturday into a rest day changes only the user's copy.
        var rest = await WorkoutApi.PutDayAsync(user.Client, plan.Id, "sat", new { isRest = true, focus = Array.Empty<string>(), items = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.OK, rest.StatusCode);

        var templates = await user.Client.GetFromJsonAsync<List<TemplateDto>>("/api/plans/templates", TestJson.Options);
        Assert.False(templates!.Single(t => t.Key == WorkoutTemplates.GymSplit4).Days.Single(d => d.DayKey == DayKey.Sat).IsRest);
    }

    [Fact]
    public async Task New_plan_starts_with_seven_rest_days_and_the_first_plan_is_active()
    {
        var user = await factory.SignUpAsync();

        var first = await (await WorkoutApi.CreatePlanAsync(user.Client, "الأولى")).ReadAsync<PlanDetailDto>();
        var second = await (await WorkoutApi.CreatePlanAsync(user.Client, "الثانية")).ReadAsync<PlanDetailDto>();

        Assert.Equal(7, first.Days.Count);
        Assert.All(first.Days, d => Assert.True(d.IsRest));
        Assert.Equal(DayKey.Sat, first.Days[0].DayKey);
        Assert.True(first.IsActive);
        Assert.False(second.IsActive);
    }

    [Fact]
    public async Task Activating_a_plan_deactivates_the_others()
    {
        var user = await factory.SignUpAsync();
        var first = await (await WorkoutApi.CreatePlanAsync(user.Client, "الأولى")).ReadAsync<PlanDetailDto>();
        var second = await (await WorkoutApi.CreatePlanAsync(user.Client, "الثانية")).ReadAsync<PlanDetailDto>();

        await user.Client.PostAsync($"/api/plans/{second.Id}/activate", null);

        var plans = await user.Client.GetFromJsonAsync<List<PlanSummaryDto>>("/api/plans", TestJson.Options);
        Assert.True(plans!.Single(p => p.Id == second.Id).IsActive);
        Assert.False(plans!.Single(p => p.Id == first.Id).IsActive);
    }

    [Fact]
    public async Task Plan_can_be_renamed_and_deleted()
    {
        var user = await factory.SignUpAsync();
        var plan = await (await WorkoutApi.CreatePlanAsync(user.Client)).ReadAsync<PlanDetailDto>();

        var renamed = await (await user.Client.PutAsJsonAsync($"/api/plans/{plan.Id}", new { name = "خطة جديدة", place = "home" }))
            .ReadAsync<PlanDetailDto>();
        Assert.Equal("خطة جديدة", renamed.Name);
        Assert.Equal(WorkoutPlace.Home, renamed.Place);

        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.DeleteAsync($"/api/plans/{plan.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.GetAsync($"/api/plans/{plan.Id}")).StatusCode);
    }

    [Fact]
    public async Task Day_exercises_keep_the_order_sent_and_can_be_reordered()
    {
        var user = await factory.SignUpAsync();
        var plan = await (await WorkoutApi.CreatePlanAsync(user.Client)).ReadAsync<PlanDetailDto>();
        var bench = await WorkoutApi.SystemExerciseAsync(user.Client, ExerciseSeedData.FlatBenchPress);
        var fly = await WorkoutApi.SystemExerciseAsync(user.Client, ExerciseSeedData.DumbbellFly);

        var day = await (await WorkoutApi.PutDayAsync(user.Client, plan.Id, "sat",
            WorkoutApi.TrainingDay((bench.Id, 4, 8, 60), (fly.Id, 3, 12, null)))).ReadAsync<PlanDayDto>();
        Assert.False(day.IsRest);
        Assert.Equal([MuscleGroup.Chest], day.Focus);
        Assert.Equal([bench.Id, fly.Id], day.Items.Select(i => i.Exercise.Id));
        Assert.Equal(60, day.Items[0].WeightKg);

        var reordered = await (await WorkoutApi.PutDayAsync(user.Client, plan.Id, "SAT",
            WorkoutApi.TrainingDay((fly.Id, 3, 12, null), (bench.Id, 4, 8, 60)))).ReadAsync<PlanDayDto>();
        Assert.Equal([fly.Id, bench.Id], reordered.Items.Select(i => i.Exercise.Id));
    }

    [Theory]
    [InlineData(21, 10, 50.0, null)]                 // sets above 20
    [InlineData(3, 101, 50.0, null)]                 // reps above 100
    [InlineData(3, 10, 501.0, null)]                 // weight above 500 kg
    [InlineData(3, 10, 50.0, "long")]                // notes above 300 characters
    public async Task Day_items_are_validated(int sets, int reps, double weight, string? notes)
    {
        var user = await factory.SignUpAsync();
        var plan = await (await WorkoutApi.CreatePlanAsync(user.Client)).ReadAsync<PlanDetailDto>();
        var bench = await WorkoutApi.SystemExerciseAsync(user.Client, ExerciseSeedData.FlatBenchPress);

        var response = await WorkoutApi.PutDayAsync(user.Client, plan.Id, "sun", new
        {
            isRest = false,
            focus = new[] { "chest" },
            items = new[] { new { exerciseId = bench.Id, sets, reps, weightKg = weight, notes = notes is null ? null : new string('x', 301) } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rest_day_cannot_contain_exercises_and_day_keys_are_checked()
    {
        var user = await factory.SignUpAsync();
        var plan = await (await WorkoutApi.CreatePlanAsync(user.Client)).ReadAsync<PlanDetailDto>();
        var bench = await WorkoutApi.SystemExerciseAsync(user.Client, ExerciseSeedData.FlatBenchPress);

        var restWithItems = await WorkoutApi.PutDayAsync(user.Client, plan.Id, "mon", new
        {
            isRest = true,
            focus = Array.Empty<string>(),
            items = new[] { new { exerciseId = bench.Id, sets = 3, reps = 10 } },
        });
        var badDay = await WorkoutApi.PutDayAsync(user.Client, plan.Id, "funday", WorkoutApi.TrainingDay((bench.Id, 3, 10, null)));
        var numericDay = await WorkoutApi.PutDayAsync(user.Client, plan.Id, "3", WorkoutApi.TrainingDay((bench.Id, 3, 10, null)));
        var unknownExercise = await WorkoutApi.PutDayAsync(user.Client, plan.Id, "mon", WorkoutApi.TrainingDay((999_999, 3, 10, null)));

        Assert.Equal(HttpStatusCode.BadRequest, restWithItems.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badDay.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, numericDay.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownExercise.StatusCode);
    }

    // ---------- Own exercises ----------

    [Fact]
    public async Task User_can_create_edit_and_delete_their_own_exercise()
    {
        var user = await factory.SignUpAsync();
        var created = await (await WorkoutApi.CreateExerciseAsync(user.Client, "ضغط بالدمبل على الأرض")).ReadAsync<ExerciseDto>();
        Assert.False(created.IsSystem);
        Assert.Contains(await WorkoutApi.ExercisesAsync(user.Client, "?mine=true"), e => e.Id == created.Id);

        var edited = await (await user.Client.PutAsJsonAsync($"/api/exercises/{created.Id}", new
        {
            name = "ضغط أرضي",
            muscleGroup = "triceps",
            region = "whole",
            place = "gym",
            defaultSets = 4,
            defaultReps = 8,
        })).ReadAsync<ExerciseDto>();
        Assert.Equal("ضغط أرضي", edited.Name);
        Assert.Equal(MuscleGroup.Triceps, edited.MuscleGroup);

        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.DeleteAsync($"/api/exercises/{created.Id}")).StatusCode);
        Assert.DoesNotContain(await WorkoutApi.ExercisesAsync(user.Client, "?mine=true"), e => e.Id == created.Id);
    }

    [Theory]
    [InlineData("x", 3)]       // name too short
    [InlineData("تمرين", 0)]   // sets below 1
    [InlineData("تمرين", 21)]  // sets above 20
    public async Task Exercise_input_is_validated(string name, int sets)
    {
        var user = await factory.SignUpAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await WorkoutApi.CreateExerciseAsync(user.Client, name, sets)).StatusCode);
    }

    [Fact]
    public async Task System_exercises_are_read_only_but_can_be_cloned_and_edited()
    {
        var user = await factory.SignUpAsync();
        var bench = await WorkoutApi.SystemExerciseAsync(user.Client, ExerciseSeedData.FlatBenchPress);

        var edit = await user.Client.PutAsJsonAsync($"/api/exercises/{bench.Id}", new
        {
            name = "تغيير", muscleGroup = "chest", region = "middle", place = "gym", defaultSets = 3, defaultReps = 10,
        });
        var delete = await user.Client.DeleteAsync($"/api/exercises/{bench.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);

        var clone = await (await user.Client.PostAsync($"/api/exercises/{bench.Id}/clone", null)).ReadAsync<ExerciseDto>();
        Assert.False(clone.IsSystem);
        Assert.Equal(bench.Name, clone.Name);
        var cloneEdit = await user.Client.PutAsJsonAsync($"/api/exercises/{clone.Id}", new
        {
            name = "بنش بقبضة ضيقة", muscleGroup = "triceps", region = "whole", place = "gym", defaultSets = 3, defaultReps = 10,
        });
        Assert.Equal(HttpStatusCode.OK, cloneEdit.StatusCode);

        var original = await WorkoutApi.SystemExerciseAsync(user.Client, ExerciseSeedData.FlatBenchPress);
        Assert.Equal(MuscleGroup.Chest, original.MuscleGroup);
    }

    [Fact]
    public async Task Deleting_an_exercise_used_in_a_plan_archives_it_and_the_plan_keeps_it()
    {
        var user = await factory.SignUpAsync();
        var mine = await (await WorkoutApi.CreateExerciseAsync(user.Client)).ReadAsync<ExerciseDto>();
        var plan = await (await WorkoutApi.CreatePlanAsync(user.Client)).ReadAsync<PlanDetailDto>();
        await WorkoutApi.PutDayAsync(user.Client, plan.Id, "tue", WorkoutApi.TrainingDay((mine.Id, 3, 10, null)));

        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.DeleteAsync($"/api/exercises/{mine.Id}")).StatusCode);

        Assert.DoesNotContain(await WorkoutApi.ExercisesAsync(user.Client, "?mine=true"), e => e.Id == mine.Id);
        var detail = await user.Client.GetFromJsonAsync<PlanDetailDto>($"/api/plans/{plan.Id}", TestJson.Options);
        var item = detail!.Days.Single(d => d.DayKey == DayKey.Tue).Items.Single();
        Assert.Equal(mine.Id, item.Exercise.Id);
        Assert.True(item.Exercise.IsArchived);

        // It can stay on that day, but it cannot be added to another day.
        var other = await WorkoutApi.PutDayAsync(user.Client, plan.Id, "thu", WorkoutApi.TrainingDay((mine.Id, 3, 10, null)));
        var same = await WorkoutApi.PutDayAsync(user.Client, plan.Id, "tue", WorkoutApi.TrainingDay((mine.Id, 4, 10, null)));
        Assert.Equal(HttpStatusCode.BadRequest, other.StatusCode);
        Assert.Equal(HttpStatusCode.OK, same.StatusCode);
    }

    // ---------- Sessions ----------

    [Fact]
    public async Task Workout_session_flow_remembers_weights_for_next_time()
    {
        var user = await factory.SignUpAsync();
        var plan = await (await WorkoutApi.ApplyTemplateAsync(user.Client)).ReadAsync<PlanDetailDto>();
        var saturday = plan.Days.Single(d => d.DayKey == DayKey.Sat);
        var bench = saturday.Items[0].Exercise;

        var session = await (await WorkoutApi.StartSessionAsync(user.Client, UserApi.DaysAgo(2), saturday.Id)).ReadAsync<SessionDto>();
        Assert.Equal(5, session.Exercises.Count);
        Assert.Null(session.Exercises[0].Last);
        Assert.Equal(plan.Name, session.PlanName);

        // Opening the same day again returns the same unfinished session.
        var again = await (await WorkoutApi.StartSessionAsync(user.Client, UserApi.DaysAgo(2), saturday.Id)).ReadAsync<SessionDto>();
        Assert.Equal(session.Id, again.Id);

        var saved = await (await WorkoutApi.SaveSetsAsync(user.Client, session.Id,
            (bench.Id, 1, 8, 60, true), (bench.Id, 2, 8, 62.5, true), (bench.Id, 3, 6, 65, false))).ReadAsync<SessionDto>();
        Assert.Equal(3, saved.Sets.Count);

        var completed = await (await user.Client.PostAsync($"/api/workouts/sessions/{session.Id}/complete", null)).ReadAsync<SessionDto>();
        Assert.NotNull(completed.CompletedAtUtc);

        // Completed sessions are locked.
        var late = await WorkoutApi.SaveSetsAsync(user.Client, session.Id, (bench.Id, 1, 8, 70, true));
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);

        // Next time, the plan and a new session show the top completed set (62.5 kg, not the failed 65 kg).
        var detail = await user.Client.GetFromJsonAsync<PlanDetailDto>($"/api/plans/{plan.Id}", TestJson.Options);
        var last = detail!.Days.Single(d => d.DayKey == DayKey.Sat).Items[0].Last;
        Assert.NotNull(last);
        Assert.Equal(62.5, last.WeightKg);
        Assert.Equal(8, last.Reps);

        var next = await (await WorkoutApi.StartSessionAsync(user.Client, UserApi.Today, saturday.Id)).ReadAsync<SessionDto>();
        Assert.NotEqual(session.Id, next.Id);
        Assert.Equal(62.5, next.Exercises[0].Last!.WeightKg);

        var history = await user.Client.GetFromJsonAsync<List<WorkoutHistoryItemDto>>($"/api/workouts/history?days=7&today={UserApi.Today}", TestJson.Options);
        var done = history!.Single(h => h.Id == session.Id);
        Assert.Equal(DayKey.Sat, done.DayKey);
        Assert.Equal(2, done.CompletedSets);
        Assert.Equal(60 * 8 + 62.5 * 8, done.TotalVolumeKg);
        Assert.Equal(next.Id, history![0].Id); // newest first
    }

    [Fact]
    public async Task Free_workout_without_a_plan_is_allowed()
    {
        var user = await factory.SignUpAsync();
        var squat = await WorkoutApi.SystemExerciseAsync(user.Client, ExerciseSeedData.Squats);

        var session = await (await WorkoutApi.StartSessionAsync(user.Client, UserApi.Today, null)).ReadAsync<SessionDto>();
        var saved = await (await WorkoutApi.SaveSetsAsync(user.Client, session.Id, (squat.Id, 1, 15, null, true), (squat.Id, 2, 12, null, true)))
            .ReadAsync<SessionDto>();

        Assert.Empty(session.Exercises);
        var logged = Assert.Single(saved.Exercises);
        Assert.Equal(squat.Id, logged.Exercise.Id);
        Assert.Equal(2, logged.PlannedSets);
    }

    [Fact]
    public async Task Set_logs_are_validated()
    {
        var user = await factory.SignUpAsync();
        var squat = await WorkoutApi.SystemExerciseAsync(user.Client, ExerciseSeedData.Squats);
        var session = await (await WorkoutApi.StartSessionAsync(user.Client, UserApi.Today, null)).ReadAsync<SessionDto>();

        var duplicate = await WorkoutApi.SaveSetsAsync(user.Client, session.Id, (squat.Id, 1, 10, null, true), (squat.Id, 1, 12, null, true));
        var tooHeavy = await WorkoutApi.SaveSetsAsync(user.Client, session.Id, (squat.Id, 1, 10, 600, true));
        var unknown = await WorkoutApi.SaveSetsAsync(user.Client, session.Id, (999_999, 1, 10, null, true));
        var badDate = await WorkoutApi.StartSessionAsync(user.Client, "2020/01/01", null);

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooHeavy.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badDate.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_plan_keeps_the_workout_history()
    {
        var user = await factory.SignUpAsync();
        var plan = await (await WorkoutApi.ApplyTemplateAsync(user.Client, WorkoutTemplates.HomeBeginner3)).ReadAsync<PlanDetailDto>();
        var day = plan.Days.First(d => !d.IsRest);
        var session = await (await WorkoutApi.StartSessionAsync(user.Client, UserApi.Today, day.Id)).ReadAsync<SessionDto>();
        await user.Client.PostAsync($"/api/workouts/sessions/{session.Id}/complete", null);

        await user.Client.DeleteAsync($"/api/plans/{plan.Id}");

        var history = await user.Client.GetFromJsonAsync<List<WorkoutHistoryItemDto>>("/api/workouts/history", TestJson.Options);
        var kept = history!.Single(h => h.Id == session.Id);
        Assert.NotNull(kept.CompletedAtUtc);
        Assert.Null(kept.PlanName);
    }

    [Theory]
    [InlineData("GET", "/api/exercises")]
    [InlineData("POST", "/api/exercises")]
    [InlineData("GET", "/api/plans")]
    [InlineData("GET", "/api/plans/templates")]
    [InlineData("POST", "/api/plans/from-template/gym-split-4")]
    [InlineData("POST", "/api/workouts/sessions")]
    [InlineData("GET", "/api/workouts/history")]
    public async Task Workout_endpoints_require_authentication(string method, string url)
    {
        var anonymous = factory.CreateHttpsClient();

        var response = await anonymous.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
