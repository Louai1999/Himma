using Himma.Api.Domain;
using static Himma.Api.Data.Seed.ExerciseSeedData;

namespace Himma.Api.Features.Workouts;

public sealed record TemplateItem(string ExerciseName, int Sets, int Reps, int RestSeconds);

public sealed record TemplateDay(DayKey DayKey, IReadOnlyList<MuscleGroup> Focus, IReadOnlyList<TemplateItem> Items);

/// <summary>Days that are not listed are rest days.</summary>
public sealed record WorkoutTemplate(
    string Key, string Name, string NameEn, string Description, string DescriptionEn, WorkoutPlace Place, IReadOnlyList<TemplateDay> Days);

/// <summary>
/// Ready plans for beginners. They live in code and are read-only; applying one copies it
/// into the user's own plan, so editing that plan never changes the template.
/// </summary>
public static class WorkoutTemplates
{
    public const string HomeBeginner3 = "home-beginner-3";
    public const string GymSplit4 = "gym-split-4";

    private const int HomeRest = 45;
    private const int GymRest = 90;

    public static IReadOnlyList<WorkoutTemplate> All { get; } =
    [
        new(HomeBeginner3,
            "مبتدئ منزلي - 3 أيام", "Home beginner - 3 days",
            "تمارين بوزن الجسم لكامل الجسم، ثلاثة أيام في الأسبوع مع يوم راحة بينها.",
            "Full-body bodyweight training, three days a week with a rest day in between.",
            WorkoutPlace.Home,
        [
            new(DayKey.Sat, [MuscleGroup.FullBody],
            [
                new(Squats, 3, 12, HomeRest),
                new(PushUps, 3, 10, HomeRest),
                new(GluteBridge, 3, 12, HomeRest),
                new(Plank, 3, 30, HomeRest),
            ]),
            new(DayKey.Mon, [MuscleGroup.FullBody, MuscleGroup.Cardio],
            [
                new(Lunges, 3, 10, HomeRest),
                new(PushUps, 3, 10, HomeRest),
                new(MountainClimbers, 3, 20, HomeRest),
                new(Plank, 3, 30, HomeRest),
            ]),
            new(DayKey.Wed, [MuscleGroup.FullBody, MuscleGroup.Cardio],
            [
                new(JumpingJacks, 3, 30, HomeRest),
                new(Squats, 3, 12, HomeRest),
                new(Burpees, 3, 8, HomeRest),
                new(GluteBridge, 3, 12, HomeRest),
            ]),
        ]),

        new(GymSplit4,
            "تقسيم الجيم - 4 أيام", "Gym split - 4 days",
            "صدر وترايسبس / ظهر وبايسبس / أرجل / أكتاف وبطن.",
            "Chest + triceps / back + biceps / legs / shoulders + abs.",
            WorkoutPlace.Gym,
        [
            new(DayKey.Sat, [MuscleGroup.Chest, MuscleGroup.Triceps],
            [
                new(FlatBenchPress, 4, 8, GymRest),
                new(InclineDumbbellPress, 3, 10, GymRest),
                new(HighCableCrossover, 3, 12, GymRest),
                new(TricepsPushdown, 3, 12, GymRest),
                new(OverheadExtension, 3, 12, GymRest),
            ]),
            new(DayKey.Sun, [MuscleGroup.Back, MuscleGroup.Biceps],
            [
                new(LatPulldown, 3, 10, GymRest),
                new(BarbellRow, 4, 8, GymRest),
                new(SeatedCableRow, 3, 10, GymRest),
                new(BarbellCurl, 3, 10, GymRest),
                new(HammerCurl, 3, 12, GymRest),
            ]),
            new(DayKey.Tue, [MuscleGroup.Legs],
            [
                new(BarbellSquat, 4, 8, GymRest),
                new(LegPress, 3, 10, GymRest),
                new(LegCurl, 3, 12, GymRest),
                new(LegExtension, 3, 12, GymRest),
                new(StandingCalfRaise, 4, 15, GymRest),
            ]),
            new(DayKey.Wed, [MuscleGroup.Shoulders, MuscleGroup.Abs],
            [
                new(DumbbellShoulderPress, 3, 10, GymRest),
                new(LateralRaise, 3, 12, GymRest),
                new(ReverseFly, 3, 12, GymRest),
                new(HangingLegRaise, 3, 10, GymRest),
                new(Crunch, 3, 15, GymRest),
            ]),
        ]),
    ];

    public static WorkoutTemplate? Find(string key) =>
        All.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));
}
