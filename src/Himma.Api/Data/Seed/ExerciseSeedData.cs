using Himma.Api.Domain;
using Himma.Api.Domain.Entities;

namespace Himma.Api.Data.Seed;

/// <summary>
/// System exercises (read-only, OwnerUserId = null). Names are unique and act as the seed key,
/// and the workout templates refer to exercises by these names.
///
/// Region meaning per muscle group (the UI shows matching labels):
///   chest / back / abs: upper, middle, lower part of the muscle;
///   legs: upper = thighs and glutes, lower = calves;
///   shoulders: upper = front head, middle = side head, lower = rear head;
///   whole = the muscle as a whole (compound movements).
/// </summary>
public static class ExerciseSeedData
{
    // Home (beginner)
    public const string PushUps = "ضغط";
    public const string Squats = "سكوات";
    public const string Lunges = "طعنات";
    public const string Plank = "بلانك";
    public const string JumpingJacks = "قفز فتح وضم";
    public const string MountainClimbers = "متسلق الجبال";
    public const string GluteBridge = "جسر الأرداف";
    public const string Burpees = "بيربي";

    // Gym: chest
    public const string FlatBenchPress = "بنش برس مستوي بالبار";
    public const string InclineDumbbellPress = "بنش برس مائل بالدمبل";
    public const string DeclineBenchPress = "بنش برس منحدر بالبار";
    public const string DumbbellFly = "تفتيح بالدمبل";
    public const string HighCableCrossover = "كروس أوفر بالكابل من الأعلى";
    public const string LowCableFly = "تفتيح بالكابل من الأسفل";
    public const string ChestDips = "متوازي للصدر";

    // Gym: back
    public const string LatPulldown = "سحب أمامي بالجهاز";
    public const string PullUps = "عقلة";
    public const string BarbellRow = "تجديف بالبار";
    public const string SeatedCableRow = "تجديف بالكابل جالساً";
    public const string OneArmDumbbellRow = "تجديف بالدمبل بيد واحدة";
    public const string Deadlift = "رفعة ميتة";
    public const string BackExtension = "تمديد أسفل الظهر";

    // Gym: legs
    public const string BarbellSquat = "سكوات بالبار";
    public const string LegPress = "دفع الأرجل بالجهاز";
    public const string LegExtension = "رفرفة أمامي للأرجل";
    public const string LegCurl = "رفرفة خلفي للأرجل";
    public const string RomanianDeadlift = "رفعة ميتة رومانية";
    public const string WalkingLunges = "طعنات بالدمبل";
    public const string StandingCalfRaise = "رفع السمانة واقفاً";

    // Gym: shoulders
    public const string DumbbellShoulderPress = "ضغط أكتاف بالدمبل";
    public const string MilitaryPress = "ضغط عسكري بالبار";
    public const string LateralRaise = "رفرفة جانبي";
    public const string FrontRaise = "رفرفة أمامي بالدمبل";
    public const string ReverseFly = "رفرفة خلفي للأكتاف";

    // Gym: triceps
    public const string TricepsPushdown = "سحب الكابل للترايسبس";
    public const string OverheadExtension = "تمديد الترايسبس فوق الرأس";
    public const string SkullCrusher = "سكل كراشر";
    public const string BenchDips = "غطس على المقعد";

    // Gym: biceps
    public const string BarbellCurl = "بايسبس بالبار";
    public const string AlternatingCurl = "بايسبس بالدمبل بالتبادل";
    public const string HammerCurl = "هامر بالدمبل";
    public const string ConcentrationCurl = "بايسبس مركز";

    // Gym: abs
    public const string Crunch = "كرانش";
    public const string HangingLegRaise = "رفع الأرجل معلقاً";
    public const string SidePlank = "بلانك جانبي";
    public const string CableCrunch = "كرانش بالكابل";
    public const string RussianTwist = "لف روسي";

    public static IReadOnlyList<ExerciseDefinition> All =>
    [
        E(PushUps, MuscleGroup.Chest, BodyRegion.Middle, WorkoutPlace.Home, "بدون أدوات", "push-up", 3, 10, "الجسم مستقيم، انزل حتى يقترب الصدر من الأرض ثم ادفع للأعلى. للمبتدئ: على الركبتين."),
        E(Squats, MuscleGroup.Legs, BodyRegion.Whole, WorkoutPlace.Home, "بدون أدوات", "squat", 3, 12, "القدمان بعرض الكتفين، انزل كأنك تجلس على كرسي مع ظهر مستقيم."),
        E(Lunges, MuscleGroup.Legs, BodyRegion.Whole, WorkoutPlace.Home, "بدون أدوات", "lunge", 3, 10, "خطوة للأمام وانزل حتى تكون الركبتان بزاوية قائمة، بالتبادل بين الرجلين."),
        E(Plank, MuscleGroup.Abs, BodyRegion.Whole, WorkoutPlace.Home, "بدون أدوات", "plank", 3, 30, "ثبات على الساعدين والجسم مستقيم. العدد هنا بالثواني."),
        E(JumpingJacks, MuscleGroup.Cardio, BodyRegion.Whole, WorkoutPlace.Home, "بدون أدوات", "jumping-jack", 3, 30, "اقفز مع فتح الذراعين والرجلين ثم الضم، بإيقاع ثابت."),
        E(MountainClimbers, MuscleGroup.Cardio, BodyRegion.Whole, WorkoutPlace.Home, "بدون أدوات", "mountain-climber", 3, 20, "من وضع الضغط، اسحب الركبتين نحو الصدر بالتبادل بسرعة."),
        E(GluteBridge, MuscleGroup.Legs, BodyRegion.Upper, WorkoutPlace.Home, "بدون أدوات", "glute-bridge", 3, 12, "استلقِ على ظهرك وارفع الحوض مع شد الأرداف، ثم انزل ببطء."),
        E(Burpees, MuscleGroup.FullBody, BodyRegion.Whole, WorkoutPlace.Home, "بدون أدوات", "burpee", 3, 8, "انزل لوضع الضغط، ارجع للوقوف واقفز للأعلى."),

        E(FlatBenchPress, MuscleGroup.Chest, BodyRegion.Middle, WorkoutPlace.Gym, "بار ومقعد", "bench-press", 4, 8, "أنزل البار إلى منتصف الصدر بتحكم ثم ادفعه للأعلى."),
        E(InclineDumbbellPress, MuscleGroup.Chest, BodyRegion.Upper, WorkoutPlace.Gym, "دمبل ومقعد مائل", "incline-press", 3, 10, "المقعد بزاوية 30 درجة تقريباً، ادفع الدمبلز للأعلى فوق الصدر."),
        E(DeclineBenchPress, MuscleGroup.Chest, BodyRegion.Lower, WorkoutPlace.Gym, "بار ومقعد منحدر", "decline-press", 3, 10, "على مقعد منحدر، أنزل البار إلى أسفل الصدر ثم ادفع."),
        E(DumbbellFly, MuscleGroup.Chest, BodyRegion.Middle, WorkoutPlace.Gym, "دمبل ومقعد", "fly", 3, 12, "افتح الذراعين بانحناء خفيف في المرفق حتى تشعر بتمدد الصدر ثم اضمم."),
        E(HighCableCrossover, MuscleGroup.Chest, BodyRegion.Lower, WorkoutPlace.Gym, "كابل", "cable-fly", 3, 12, "اسحب الكابلين من الأعلى إلى الأسفل أمام الجسم."),
        E(LowCableFly, MuscleGroup.Chest, BodyRegion.Upper, WorkoutPlace.Gym, "كابل", "cable-fly", 3, 12, "اسحب الكابلين من الأسفل إلى مستوى الكتفين."),
        E(ChestDips, MuscleGroup.Chest, BodyRegion.Lower, WorkoutPlace.Gym, "متوازي", "dip", 3, 8, "مِل بالجسم للأمام وانزل ببطء ثم ادفع للأعلى."),

        E(LatPulldown, MuscleGroup.Back, BodyRegion.Upper, WorkoutPlace.Gym, "جهاز سحب", "pulldown", 3, 10, "اسحب البار إلى أعلى الصدر مع ضم لوحي الكتف."),
        E(PullUps, MuscleGroup.Back, BodyRegion.Upper, WorkoutPlace.Gym, "عقلة", "pull-up", 3, 6, "اسحب جسمك حتى يتجاوز الذقن البار. للمبتدئ: استخدم جهاز المساعدة."),
        E(BarbellRow, MuscleGroup.Back, BodyRegion.Middle, WorkoutPlace.Gym, "بار", "row", 4, 8, "انحنِ بظهر مستقيم واسحب البار نحو البطن."),
        E(SeatedCableRow, MuscleGroup.Back, BodyRegion.Middle, WorkoutPlace.Gym, "كابل", "row", 3, 10, "اجلس بظهر مستقيم واسحب المقبض نحو البطن."),
        E(OneArmDumbbellRow, MuscleGroup.Back, BodyRegion.Middle, WorkoutPlace.Gym, "دمبل ومقعد", "row", 3, 10, "اسند يداً وركبة على المقعد واسحب الدمبل نحو الخصر."),
        E(Deadlift, MuscleGroup.Back, BodyRegion.Lower, WorkoutPlace.Gym, "بار", "deadlift", 3, 5, "ظهر مستقيم والبار قريب من الساقين، ارفع بدفع الأرض بالقدمين."),
        E(BackExtension, MuscleGroup.Back, BodyRegion.Lower, WorkoutPlace.Gym, "جهاز تمديد الظهر", "back-extension", 3, 12, "انزل بالجذع ببطء ثم ارجع حتى يستقيم الجسم دون مبالغة."),

        E(BarbellSquat, MuscleGroup.Legs, BodyRegion.Whole, WorkoutPlace.Gym, "بار وحامل", "squat", 4, 8, "البار على أعلى الظهر، انزل حتى يوازي الفخذ الأرض."),
        E(LegPress, MuscleGroup.Legs, BodyRegion.Whole, WorkoutPlace.Gym, "جهاز دفع الأرجل", "leg-press", 3, 10, "ادفع المنصة دون قفل الركبتين في الأعلى."),
        E(LegExtension, MuscleGroup.Legs, BodyRegion.Upper, WorkoutPlace.Gym, "جهاز", "leg-extension", 3, 12, "مد الرجلين حتى الاستقامة ثم انزل ببطء."),
        E(LegCurl, MuscleGroup.Legs, BodyRegion.Upper, WorkoutPlace.Gym, "جهاز", "leg-curl", 3, 12, "اثنِ الركبتين لسحب الوسادة نحو الأرداف."),
        E(RomanianDeadlift, MuscleGroup.Legs, BodyRegion.Upper, WorkoutPlace.Gym, "بار أو دمبل", "romanian-deadlift", 3, 10, "انحنِ من الحوض مع ركبتين شبه مستقيمتين حتى تشعر بشد خلف الفخذ."),
        E(WalkingLunges, MuscleGroup.Legs, BodyRegion.Whole, WorkoutPlace.Gym, "دمبل", "lunge", 3, 10, "امشِ بخطوات طعن واسعة مع الدمبلز على الجانبين."),
        E(StandingCalfRaise, MuscleGroup.Legs, BodyRegion.Lower, WorkoutPlace.Gym, "جهاز أو دمبل", "calf-raise", 4, 15, "ارتفع على أطراف الأصابع ثم انزل ببطء."),

        E(DumbbellShoulderPress, MuscleGroup.Shoulders, BodyRegion.Whole, WorkoutPlace.Gym, "دمبل ومقعد", "shoulder-press", 3, 10, "ادفع الدمبلز من مستوى الأذنين إلى فوق الرأس."),
        E(MilitaryPress, MuscleGroup.Shoulders, BodyRegion.Whole, WorkoutPlace.Gym, "بار", "shoulder-press", 3, 8, "واقفاً، ادفع البار من أعلى الصدر إلى فوق الرأس."),
        E(LateralRaise, MuscleGroup.Shoulders, BodyRegion.Middle, WorkoutPlace.Gym, "دمبل", "lateral-raise", 3, 12, "ارفع الذراعين جانباً حتى مستوى الكتفين."),
        E(FrontRaise, MuscleGroup.Shoulders, BodyRegion.Upper, WorkoutPlace.Gym, "دمبل", "front-raise", 3, 12, "ارفع الدمبل أمامك حتى مستوى الكتف."),
        E(ReverseFly, MuscleGroup.Shoulders, BodyRegion.Lower, WorkoutPlace.Gym, "دمبل أو جهاز", "reverse-fly", 3, 12, "انحنِ للأمام وافتح الذراعين للخلف مع ضم لوحي الكتف."),

        E(TricepsPushdown, MuscleGroup.Triceps, BodyRegion.Whole, WorkoutPlace.Gym, "كابل", "pushdown", 3, 12, "المرفقان ثابتان بجانب الجسم، ادفع الحبل للأسفل."),
        E(OverheadExtension, MuscleGroup.Triceps, BodyRegion.Whole, WorkoutPlace.Gym, "دمبل", "overhead-extension", 3, 12, "أنزل الدمبل خلف الرأس ثم مد الذراعين."),
        E(SkullCrusher, MuscleGroup.Triceps, BodyRegion.Whole, WorkoutPlace.Gym, "بار EZ ومقعد", "skull-crusher", 3, 10, "مستلقياً، أنزل البار نحو الجبهة بثني المرفقين ثم مد."),
        E(BenchDips, MuscleGroup.Triceps, BodyRegion.Whole, WorkoutPlace.Gym, "مقعد", "dip", 3, 10, "اليدان على حافة المقعد، انزل بثني المرفقين ثم ادفع."),

        E(BarbellCurl, MuscleGroup.Biceps, BodyRegion.Whole, WorkoutPlace.Gym, "بار", "curl", 3, 10, "المرفقان ثابتان، ارفع البار نحو الكتفين ثم انزل ببطء."),
        E(AlternatingCurl, MuscleGroup.Biceps, BodyRegion.Whole, WorkoutPlace.Gym, "دمبل", "curl", 3, 10, "ارفع دمبلاً واحداً كل مرة مع لف الكف للأعلى."),
        E(HammerCurl, MuscleGroup.Biceps, BodyRegion.Whole, WorkoutPlace.Gym, "دمبل", "hammer-curl", 3, 12, "قبضة متعادلة (الكفان متقابلان) وارفع الدمبلز."),
        E(ConcentrationCurl, MuscleGroup.Biceps, BodyRegion.Whole, WorkoutPlace.Gym, "دمبل", "curl", 3, 12, "جالساً والمرفق على الفخذ، ارفع الدمبل ببطء."),

        E(Crunch, MuscleGroup.Abs, BodyRegion.Upper, WorkoutPlace.Gym, "بدون أدوات", "crunch", 3, 15, "ارفع الكتفين عن الأرض بشد البطن دون سحب الرقبة."),
        E(HangingLegRaise, MuscleGroup.Abs, BodyRegion.Lower, WorkoutPlace.Gym, "عقلة", "leg-raise", 3, 10, "معلقاً، ارفع الرجلين للأمام دون تأرجح."),
        E(SidePlank, MuscleGroup.Abs, BodyRegion.Middle, WorkoutPlace.Gym, "بدون أدوات", "side-plank", 3, 30, "ثبات على ساعد واحد والجسم مستقيم. العدد بالثواني لكل جهة."),
        E(CableCrunch, MuscleGroup.Abs, BodyRegion.Upper, WorkoutPlace.Gym, "كابل", "crunch", 3, 12, "راكعاً، اسحب الحبل للأسفل بثني البطن."),
        E(RussianTwist, MuscleGroup.Abs, BodyRegion.Middle, WorkoutPlace.Gym, "بدون أدوات أو كرة", "twist", 3, 20, "جالساً مع رفع القدمين قليلاً، لف الجذع يميناً ويساراً."),
    ];

    private static ExerciseDefinition E(
        string name, MuscleGroup muscle, BodyRegion region, WorkoutPlace place,
        string equipment, string iconKey, int sets, int reps, string instructions) => new()
    {
        OwnerUserId = null,
        Name = name,
        MuscleGroup = muscle,
        Region = region,
        Place = place,
        Equipment = equipment,
        IconKey = iconKey,
        DefaultSets = sets,
        DefaultReps = reps,
        Instructions = instructions,
        CreatedAtUtc = DateTime.UtcNow,
    };
}
