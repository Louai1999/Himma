namespace Himma.Api.Data.Seed;

public static partial class ExerciseSeedData
{
    private sealed record EnglishText(string Name, string Equipment, string Instructions);

    /// <summary>English name, equipment and instructions for each system exercise, keyed by its Arabic name.</summary>
    private static readonly Dictionary<string, EnglishText> English = new()
    {
        [PushUps] = new("Push-ups", "No equipment", "Body straight, lower until your chest nearly touches the floor, then push up. Beginners: on your knees."),
        [Squats] = new("Squats", "No equipment", "Feet shoulder-width apart, sit back as if onto a chair, keep your back straight."),
        [Lunges] = new("Lunges", "No equipment", "Step forward and lower until both knees are at a right angle, alternating legs."),
        [Plank] = new("Plank", "No equipment", "Hold on your forearms with your body in a straight line. The count is in seconds."),
        [JumpingJacks] = new("Jumping jacks", "No equipment", "Jump while opening arms and legs, then close them, at a steady rhythm."),
        [MountainClimbers] = new("Mountain climbers", "No equipment", "From a push-up position, drive your knees to your chest one after the other, quickly."),
        [GluteBridge] = new("Glute bridge", "No equipment", "Lie on your back, lift your hips while squeezing your glutes, then lower slowly."),
        [Burpees] = new("Burpees", "No equipment", "Drop into a push-up position, come back up and jump."),

        [FlatBenchPress] = new("Flat barbell bench press", "Barbell and bench", "Lower the bar to mid-chest under control, then press it up."),
        [InclineDumbbellPress] = new("Incline dumbbell press", "Dumbbells and incline bench", "Bench at about 30 degrees, press the dumbbells up over your chest."),
        [DeclineBenchPress] = new("Decline barbell bench press", "Barbell and decline bench", "On a decline bench, lower the bar to your lower chest, then press."),
        [DumbbellFly] = new("Dumbbell fly", "Dumbbells and bench", "Open your arms with a slight elbow bend until your chest stretches, then bring them together."),
        [HighCableCrossover] = new("High-to-low cable crossover", "Cable", "Pull both cables from high to low in front of your body."),
        [LowCableFly] = new("Low-to-high cable fly", "Cable", "Pull both cables from low up to shoulder height."),
        [ChestDips] = new("Chest dips", "Dip bars", "Lean your body forward, lower slowly, then push up."),

        [LatPulldown] = new("Lat pulldown", "Pulldown machine", "Pull the bar to your upper chest while squeezing your shoulder blades."),
        [PullUps] = new("Pull-ups", "Pull-up bar", "Pull yourself up until your chin passes the bar. Beginners: use the assisted machine."),
        [BarbellRow] = new("Barbell row", "Barbell", "Hinge forward with a straight back and pull the bar to your belly."),
        [SeatedCableRow] = new("Seated cable row", "Cable", "Sit tall and pull the handle to your belly."),
        [OneArmDumbbellRow] = new("One-arm dumbbell row", "Dumbbell and bench", "Brace a hand and knee on the bench and pull the dumbbell to your waist."),
        [Deadlift] = new("Deadlift", "Barbell", "Straight back and the bar close to your legs; lift by pushing the floor away."),
        [BackExtension] = new("Back extension", "Back extension bench", "Lower your torso slowly, then rise until your body is straight, no further."),

        [BarbellSquat] = new("Barbell squat", "Barbell and rack", "Bar on your upper back, squat until your thighs are parallel to the floor."),
        [LegPress] = new("Leg press", "Leg press machine", "Push the platform without locking your knees at the top."),
        [LegExtension] = new("Leg extension", "Machine", "Straighten your legs fully, then lower slowly."),
        [LegCurl] = new("Leg curl", "Machine", "Bend your knees to pull the pad toward your glutes."),
        [RomanianDeadlift] = new("Romanian deadlift", "Barbell or dumbbells", "Hinge at the hips with soft knees until you feel a stretch in your hamstrings."),
        [WalkingLunges] = new("Dumbbell walking lunges", "Dumbbells", "Walk with long lunge steps, dumbbells at your sides."),
        [StandingCalfRaise] = new("Standing calf raise", "Machine or dumbbells", "Rise onto your toes, then lower slowly."),

        [DumbbellShoulderPress] = new("Dumbbell shoulder press", "Dumbbells and bench", "Press the dumbbells from ear level to overhead."),
        [MilitaryPress] = new("Military press", "Barbell", "Standing, press the bar from your upper chest to overhead."),
        [LateralRaise] = new("Lateral raise", "Dumbbells", "Raise your arms out to the sides up to shoulder height."),
        [FrontRaise] = new("Dumbbell front raise", "Dumbbells", "Raise the dumbbell in front of you up to shoulder height."),
        [ReverseFly] = new("Reverse fly", "Dumbbells or machine", "Lean forward and open your arms backward, squeezing your shoulder blades."),

        [TricepsPushdown] = new("Triceps pushdown", "Cable", "Elbows fixed at your sides, push the rope down."),
        [OverheadExtension] = new("Overhead triceps extension", "Dumbbell", "Lower the dumbbell behind your head, then straighten your arms."),
        [SkullCrusher] = new("Skull crusher", "EZ bar and bench", "Lying down, bend your elbows to lower the bar toward your forehead, then extend."),
        [BenchDips] = new("Bench dips", "Bench", "Hands on the bench edge, bend your elbows to lower, then push up."),

        [BarbellCurl] = new("Barbell curl", "Barbell", "Elbows fixed, curl the bar toward your shoulders, then lower slowly."),
        [AlternatingCurl] = new("Alternating dumbbell curl", "Dumbbells", "Curl one dumbbell at a time, turning your palm up."),
        [HammerCurl] = new("Hammer curl", "Dumbbells", "Neutral grip (palms facing each other) and curl the dumbbells."),
        [ConcentrationCurl] = new("Concentration curl", "Dumbbell", "Seated with your elbow on your thigh, curl the dumbbell slowly."),

        [Crunch] = new("Crunch", "No equipment", "Lift your shoulders off the floor by tightening your abs, without pulling your neck."),
        [HangingLegRaise] = new("Hanging leg raise", "Pull-up bar", "Hanging, raise your legs forward without swinging."),
        [SidePlank] = new("Side plank", "No equipment", "Hold on one forearm with your body straight. The count is seconds per side."),
        [CableCrunch] = new("Cable crunch", "Cable", "Kneeling, pull the rope down by curling your abs."),
        [RussianTwist] = new("Russian twist", "No equipment or a ball", "Seated with your feet slightly raised, twist your torso left and right."),
    };
}
