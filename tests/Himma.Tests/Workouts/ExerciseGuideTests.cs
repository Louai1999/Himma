using Himma.Api.Data.Seed;
using Himma.Tests.Auth;
using Himma.Tests.Tracking;

namespace Himma.Tests.Workouts;

public sealed class ExerciseGuideTests(HimmaApiFactory factory) : IClassFixture<HimmaApiFactory>
{
    [Fact]
    public void Every_system_exercise_has_a_guide()
    {
        foreach (var exercise in ExerciseSeedData.All)
        {
            var guide = ExerciseGuides.Find(exercise.Name);
            Assert.True(guide is not null, $"{exercise.Name} has no guide");
            Assert.InRange(guide.StepsAr.Count, 3, 4);
            Assert.Equal(guide.StepsAr.Count, guide.StepsEn.Count);
            Assert.False(string.IsNullOrWhiteSpace(guide.TipAr));
            Assert.False(string.IsNullOrWhiteSpace(guide.TipEn));
        }
        Assert.Equal(ExerciseSeedData.All.Count, ExerciseGuides.All.Count);
    }

    [Fact]
    public void Guide_photos_exist_in_wwwroot()
    {
        var root = FindWwwroot();
        foreach (var guide in ExerciseGuides.All.Values.Where(g => g.MediaKey is not null))
        {
            foreach (var frame in new[] { "0.jpg", "1.jpg" })
            {
                var path = Path.Combine(root, "assets", "exercises", guide.MediaKey!, frame);
                Assert.True(File.Exists(path), $"missing {path}");
            }
        }
    }

    [Fact]
    public async Task System_exercises_come_with_their_guide_in_the_request_language()
    {
        var user = await factory.SignUpAsync();
        var bench = await WorkoutApi.SystemExerciseAsync(user.Client, ExerciseSeedData.FlatBenchPress);

        Assert.NotNull(bench.Guide);
        Assert.Equal(["/assets/exercises/flat-bench-press/0.jpg", "/assets/exercises/flat-bench-press/1.jpg"], bench.Guide.Images);
        Assert.Contains("الترايسبس", bench.Guide.HelperMuscles);
        Assert.Equal(4, bench.Guide.Steps.Count);

        user.Client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");
        var english = (await WorkoutApi.ExercisesAsync(user.Client)).Single(e => e.Id == bench.Id);
        Assert.Contains("Triceps", english.Guide!.HelperMuscles);
        Assert.StartsWith("Lie on the bench", english.Guide.Steps[0]);

        var burpees = (await WorkoutApi.ExercisesAsync(user.Client, "?place=home")).Single(e => e.IsSystem && e.Name == "Burpees");
        Assert.Empty(burpees.Guide!.Images);
    }

    [Fact]
    public async Task User_exercises_have_no_guide()
    {
        var user = await factory.SignUpAsync();
        var mine = await (await WorkoutApi.CreateExerciseAsync(user.Client)).ReadAsync<Himma.Api.Features.Workouts.ExerciseDto>();

        Assert.Null(mine.Guide);
    }

    private static string FindWwwroot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Himma.Api", "wwwroot");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("src/Himma.Api/wwwroot not found");
    }
}
