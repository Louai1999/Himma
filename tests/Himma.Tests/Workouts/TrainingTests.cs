using System.Net;
using System.Net.Http.Json;
using Himma.Api.Domain;
using Himma.Api.Domain.Training;
using Himma.Api.Features.Workouts;
using Himma.Tests.Auth;
using Himma.Tests.Tracking;

namespace Himma.Tests.Workouts;

public sealed class ProgressionTests
{
    private static IReadOnlyList<PastSet> Sets(int count, int reps, double? kg, int? rir = null) =>
        Enumerable.Range(0, count).Select(_ => new PastSet(reps, kg, rir)).ToList();

    [Fact]
    public void Hitting_every_set_twice_at_the_same_weight_means_go_heavier()
    {
        var hint = Progression.Advise(MuscleGroup.Chest, 3, 8, [Sets(3, 8, 60), Sets(3, 8, 60)]);

        Assert.Equal(new ProgressionHint(ProgressionKinds.Increase, ProgressionReasons.TwoSessionsHit, 62.5, 8), hint);
    }

    [Fact]
    public void Legs_go_up_in_bigger_steps()
    {
        var hint = Progression.Advise(MuscleGroup.Legs, 3, 8, [Sets(3, 8, 100), Sets(3, 9, 100)]);

        Assert.Equal(105, hint!.WeightKg);
    }

    [Fact]
    public void One_easy_session_with_three_reps_to_spare_is_enough()
    {
        var hint = Progression.Advise(MuscleGroup.Back, 3, 10, [Sets(3, 10, 50, rir: 3)]);

        Assert.Equal(ProgressionReasons.EasyLastTime, hint!.Reason);
        Assert.Equal(52.5, hint.WeightKg);
    }

    [Fact]
    public void A_single_normal_session_or_a_new_weight_gives_no_hint_yet()
    {
        Assert.Null(Progression.Advise(MuscleGroup.Chest, 3, 8, [Sets(3, 8, 60)]));
        Assert.Null(Progression.Advise(MuscleGroup.Chest, 3, 8, [Sets(3, 8, 62.5), Sets(3, 8, 60)]));
        Assert.Null(Progression.Advise(MuscleGroup.Chest, 3, 8, []));
    }

    [Fact]
    public void Going_to_failure_does_not_count_as_hitting_the_target()
    {
        Assert.Null(Progression.Advise(MuscleGroup.Chest, 3, 8, [Sets(3, 8, 60, rir: 0), Sets(3, 8, 60, rir: 0)]));
    }

    [Fact]
    public void Missing_reps_by_two_or_more_means_keep_the_weight()
    {
        IReadOnlyList<PastSet> last = [new(8, 70, null), new(6, 70, null), new(5, 70, null)];

        var hint = Progression.Advise(MuscleGroup.Chest, 3, 8, [last]);

        Assert.Equal(new ProgressionHint(ProgressionKinds.Keep, ProgressionReasons.MissedReps, 70, 8), hint);
    }

    [Fact]
    public void Bodyweight_exercises_progress_with_more_reps()
    {
        var hint = Progression.Advise(MuscleGroup.Chest, 3, 12, [Sets(3, 12, null), Sets(3, 12, 0)]);

        Assert.Equal(new ProgressionHint(ProgressionKinds.AddReps, ProgressionReasons.TwoSessionsHit, null, 14), hint);
    }
}

public sealed class WeeklyVolumeTests
{
    [Fact]
    public void Weeks_start_on_saturday()
    {
        var sunday = new DateOnly(2026, 10, 25);

        Assert.Equal(new DateOnly(2026, 10, 24), WeeklyVolume.WeekStart(sunday));
        Assert.Equal(new DateOnly(2026, 10, 24), WeeklyVolume.WeekStart(new DateOnly(2026, 10, 30)));   // Friday
    }

    [Fact]
    public void Sets_are_counted_per_muscle_and_week_and_cardio_is_left_out()
    {
        var today = new DateOnly(2026, 10, 25);
        var sets = Enumerable.Repeat((today, MuscleGroup.Chest, 8, (double?)60), 12)
            .Concat(Enumerable.Repeat((today.AddDays(-1), MuscleGroup.Back, 10, (double?)50), 4))
            .Concat(Enumerable.Repeat((today.AddDays(-7), MuscleGroup.Chest, 8, (double?)60), 22))
            .Append((today, MuscleGroup.Cardio, 20, null));

        var weeks = WeeklyVolume.Build(sets, today, 2);

        Assert.Equal(2, weeks.Count);
        var thisWeek = weeks[1];
        Assert.Equal(new DateOnly(2026, 10, 24), thisWeek.WeekStart);
        Assert.Equal(new MuscleVolume(MuscleGroup.Chest, 12, 5760, VolumeStatus.Good), thisWeek.Muscles[0]);
        Assert.Equal(new MuscleVolume(MuscleGroup.Back, 4, 2000, VolumeStatus.Low), thisWeek.Muscles[1]);
        Assert.Equal(2, thisWeek.Muscles.Count);
        Assert.Equal(VolumeStatus.High, Assert.Single(weeks[0].Muscles).Status);
    }
}

public sealed class TrainingTests(HimmaApiFactory factory) : IClassFixture<HimmaApiFactory>
{
    private static async Task<SessionDto> SessionAsync(SignedInUser user, string date, int? planDayId,
        params (int ExerciseId, int SetNumber, int Reps, double? WeightKg, bool Completed)[] sets)
    {
        var session = await (await WorkoutApi.StartSessionAsync(user.Client, date, planDayId)).ReadAsync<SessionDto>();
        if (sets.Length > 0)
        {
            (await WorkoutApi.SaveSetsAsync(user.Client, session.Id, sets)).EnsureSuccessStatusCode();
            await user.Client.PostAsync($"/api/workouts/sessions/{session.Id}/complete", null);
        }
        return session;
    }

    [Fact]
    public async Task Rir_is_saved_with_a_set_and_validated()
    {
        var user = await factory.SignUpAsync();
        var bench = await (await WorkoutApi.CreateExerciseAsync(user.Client)).ReadAsync<ExerciseDto>();
        var session = await (await WorkoutApi.StartSessionAsync(user.Client, UserApi.Today, null)).ReadAsync<SessionDto>();

        var saved = await user.Client.PutAsJsonAsync($"/api/workouts/sessions/{session.Id}/sets", new
        {
            sets = new[] { new { exerciseId = bench.Id, setNumber = 1, reps = 8, weightKg = 60, completed = true, rir = 2 } },
        });
        var invalid = await user.Client.PutAsJsonAsync($"/api/workouts/sessions/{session.Id}/sets", new
        {
            sets = new[] { new { exerciseId = bench.Id, setNumber = 1, reps = 8, weightKg = 60, completed = true, rir = 9 } },
        });

        Assert.Equal(2, Assert.Single((await saved.ReadAsync<SessionDto>()).Sets).Rir);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task A_planned_exercise_hit_twice_shows_an_increase_hint_in_the_next_session()
    {
        var user = await factory.SignUpAsync();
        var bench = await (await WorkoutApi.CreateExerciseAsync(user.Client, "ضغط صدر")).ReadAsync<ExerciseDto>();
        var plan = await (await WorkoutApi.CreatePlanAsync(user.Client)).ReadAsync<PlanDetailDto>();
        var day = await (await WorkoutApi.PutDayAsync(user.Client, plan.Id, "sat", WorkoutApi.TrainingDay((bench.Id, 2, 8, 60))))
            .ReadAsync<PlanDayDto>();

        await SessionAsync(user, UserApi.DaysAgo(14), day.Id, (bench.Id, 1, 8, 60, true), (bench.Id, 2, 8, 60, true));
        await SessionAsync(user, UserApi.DaysAgo(7), day.Id, (bench.Id, 1, 9, 60, true), (bench.Id, 2, 8, 60, true));
        var next = await SessionAsync(user, UserApi.Today, day.Id);

        var hint = next.Exercises.Single().Hint;
        Assert.Equal(ProgressionKinds.Increase, hint!.Kind);
        Assert.Equal(62.5, hint.WeightKg);
    }

    [Fact]
    public async Task Weekly_volume_counts_completed_sets_per_muscle()
    {
        var user = await factory.SignUpAsync();
        var bench = await (await WorkoutApi.CreateExerciseAsync(user.Client)).ReadAsync<ExerciseDto>();
        await SessionAsync(user, UserApi.Today, null,
            (bench.Id, 1, 8, 60, true), (bench.Id, 2, 8, 60, true), (bench.Id, 3, 8, 60, false));

        var volume = await user.Client.GetFromJsonAsync<VolumeResponse>($"/api/workouts/volume?weeks=2&today={UserApi.Today}", TestJson.Options);

        Assert.Equal(10, volume!.TargetMinSets);
        Assert.Equal(2, volume.Weeks.Count);
        var muscle = Assert.Single(volume.Weeks[^1].Muscles);
        Assert.Equal(bench.MuscleGroup, muscle.Muscle);
        Assert.Equal(2, muscle.Sets);
        Assert.Equal(VolumeStatus.Low, muscle.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.Client.GetAsync("/api/workouts/volume?weeks=50")).StatusCode);
    }

    [Fact]
    public async Task Weekly_volume_only_counts_the_users_own_workouts()
    {
        var a = await factory.SignUpAsync();
        var b = await factory.SignUpAsync();
        var bench = await (await WorkoutApi.CreateExerciseAsync(a.Client)).ReadAsync<ExerciseDto>();
        await SessionAsync(a, UserApi.Today, null, (bench.Id, 1, 8, 60, true));

        var volume = await b.Client.GetFromJsonAsync<VolumeResponse>("/api/workouts/volume", TestJson.Options);

        Assert.All(volume!.Weeks, w => Assert.Empty(w.Muscles));
    }
}
