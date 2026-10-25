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
    public const string PushPullLegs6 = "push-pull-legs-6";
    public const string UpperLower4 = "upper-lower-4";
    public const string BroSplit5 = "bro-split-5";

    private const int HomeRest = 45;
    private const int GymRest = 90;
    /// <summary>Heavy compound lifts need a longer rest.</summary>
    private const int HeavyRest = 150;

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

        new(PushPullLegs6,
            "دفع / سحب / أرجل - 6 أيام", "Push / pull / legs - 6 days",
            "للمتقدمين: كل عضلة مرتين في الأسبوع. دفع (صدر وأكتاف وترايسبس) / سحب (ظهر وبايسبس) / أرجل، ثم تتكرر.",
            "For advanced lifters: every muscle twice a week. Push (chest, shoulders, triceps) / pull (back, biceps) / legs, then repeat.",
            WorkoutPlace.Gym,
        [
            new(DayKey.Sat, [MuscleGroup.Chest, MuscleGroup.Shoulders, MuscleGroup.Triceps], PushDay(FlatBenchPress, DumbbellShoulderPress)),
            new(DayKey.Sun, [MuscleGroup.Back, MuscleGroup.Biceps], PullDay(Deadlift)),
            new(DayKey.Mon, [MuscleGroup.Legs], LegDay(BarbellSquat)),
            new(DayKey.Tue, [MuscleGroup.Chest, MuscleGroup.Shoulders, MuscleGroup.Triceps], PushDay(InclineDumbbellPress, MilitaryPress)),
            new(DayKey.Wed, [MuscleGroup.Back, MuscleGroup.Biceps], PullDay(BarbellRow)),
            new(DayKey.Thu, [MuscleGroup.Legs], LegDay(RomanianDeadlift)),
        ]),

        new(UpperLower4,
            "علوي / سفلي - 4 أيام", "Upper / lower - 4 days",
            "توازن ممتاز بين القوة والضخامة: كل عضلة مرتين في الأسبوع مع راحة كافية.",
            "A great balance of strength and size: every muscle twice a week with enough rest.",
            WorkoutPlace.Gym,
        [
            new(DayKey.Sat, [MuscleGroup.Chest, MuscleGroup.Back, MuscleGroup.Shoulders],
            [
                new(FlatBenchPress, 4, 6, HeavyRest),
                new(BarbellRow, 4, 6, HeavyRest),
                new(DumbbellShoulderPress, 3, 10, GymRest),
                new(LatPulldown, 3, 10, GymRest),
                new(TricepsPushdown, 2, 12, GymRest),
                new(BarbellCurl, 2, 12, GymRest),
            ]),
            new(DayKey.Sun, [MuscleGroup.Legs, MuscleGroup.Abs],
            [
                new(BarbellSquat, 4, 6, HeavyRest),
                new(RomanianDeadlift, 3, 8, HeavyRest),
                new(LegPress, 3, 10, GymRest),
                new(LegCurl, 3, 12, GymRest),
                new(StandingCalfRaise, 4, 15, GymRest),
                new(HangingLegRaise, 3, 12, GymRest),
            ]),
            new(DayKey.Tue, [MuscleGroup.Chest, MuscleGroup.Back, MuscleGroup.Shoulders],
            [
                new(InclineDumbbellPress, 4, 8, GymRest),
                new(PullUps, 4, 8, GymRest),
                new(LateralRaise, 3, 15, GymRest),
                new(SeatedCableRow, 3, 10, GymRest),
                new(OverheadExtension, 2, 12, GymRest),
                new(HammerCurl, 2, 12, GymRest),
            ]),
            new(DayKey.Wed, [MuscleGroup.Legs, MuscleGroup.Abs],
            [
                new(Deadlift, 3, 5, HeavyRest),
                new(WalkingLunges, 3, 10, GymRest),
                new(LegExtension, 3, 12, GymRest),
                new(LegCurl, 3, 12, GymRest),
                new(StandingCalfRaise, 4, 15, GymRest),
                new(CableCrunch, 3, 15, GymRest),
            ]),
        ]),

        new(BroSplit5,
            "عضلة لكل يوم - 5 أيام", "Bro split - 5 days",
            "التقسيم الكلاسيكي لكمال الأجسام: صدر / ظهر / أكتاف / ذراعين / أرجل، بحجم عالٍ لكل عضلة.",
            "The classic bodybuilding split: chest / back / shoulders / arms / legs, with high volume per muscle.",
            WorkoutPlace.Gym,
        [
            new(DayKey.Sat, [MuscleGroup.Chest],
            [
                new(FlatBenchPress, 4, 8, HeavyRest),
                new(InclineDumbbellPress, 4, 10, GymRest),
                new(DeclineBenchPress, 3, 10, GymRest),
                new(DumbbellFly, 3, 12, GymRest),
                new(HighCableCrossover, 3, 15, GymRest),
            ]),
            new(DayKey.Sun, [MuscleGroup.Back],
            [
                new(Deadlift, 3, 5, HeavyRest),
                new(PullUps, 4, 8, GymRest),
                new(BarbellRow, 4, 8, GymRest),
                new(LatPulldown, 3, 10, GymRest),
                new(OneArmDumbbellRow, 3, 10, GymRest),
            ]),
            new(DayKey.Mon, [MuscleGroup.Shoulders],
            [
                new(MilitaryPress, 4, 8, HeavyRest),
                new(DumbbellShoulderPress, 3, 10, GymRest),
                new(LateralRaise, 4, 15, GymRest),
                new(FrontRaise, 3, 12, GymRest),
                new(ReverseFly, 4, 15, GymRest),
            ]),
            new(DayKey.Tue, [MuscleGroup.Biceps, MuscleGroup.Triceps],
            [
                new(BarbellCurl, 4, 10, GymRest),
                new(SkullCrusher, 4, 10, GymRest),
                new(AlternatingCurl, 3, 12, GymRest),
                new(TricepsPushdown, 3, 12, GymRest),
                new(ConcentrationCurl, 3, 12, GymRest),
                new(OverheadExtension, 3, 12, GymRest),
            ]),
            new(DayKey.Wed, [MuscleGroup.Legs],
            [
                new(BarbellSquat, 4, 8, HeavyRest),
                new(LegPress, 4, 10, GymRest),
                new(RomanianDeadlift, 3, 10, GymRest),
                new(LegExtension, 3, 15, GymRest),
                new(LegCurl, 3, 12, GymRest),
                new(StandingCalfRaise, 5, 15, GymRest),
            ]),
        ]),
    ];

    private static IReadOnlyList<TemplateItem> PushDay(string mainPress, string shoulderPress) =>
    [
        new(mainPress, 4, 8, HeavyRest),
        new(shoulderPress, 3, 10, GymRest),
        new(DumbbellFly, 3, 12, GymRest),
        new(LateralRaise, 3, 15, GymRest),
        new(TricepsPushdown, 3, 12, GymRest),
        new(OverheadExtension, 3, 12, GymRest),
    ];

    private static IReadOnlyList<TemplateItem> PullDay(string mainPull) =>
    [
        new(mainPull, 4, mainPull == Deadlift ? 5 : 8, HeavyRest),
        new(PullUps, 3, 8, GymRest),
        new(SeatedCableRow, 3, 10, GymRest),
        new(ReverseFly, 3, 15, GymRest),
        new(BarbellCurl, 3, 10, GymRest),
        new(HammerCurl, 3, 12, GymRest),
    ];

    private static IReadOnlyList<TemplateItem> LegDay(string mainLift) =>
    [
        new(mainLift, 4, mainLift == BarbellSquat ? 6 : 8, HeavyRest),
        new(LegPress, 3, 10, GymRest),
        new(LegCurl, 3, 12, GymRest),
        new(LegExtension, 3, 12, GymRest),
        new(StandingCalfRaise, 4, 15, GymRest),
        new(Crunch, 3, 15, GymRest),
    ];

    public static WorkoutTemplate? Find(string key) =>
        All.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));
}
