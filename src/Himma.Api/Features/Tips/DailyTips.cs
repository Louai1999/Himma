using Himma.Api.Infrastructure.Localization;

namespace Himma.Api.Features.Tips;

/// <summary>Short motivational tips. The tip of the day is chosen by the day of the year.</summary>
public static class DailyTips
{
    public static IReadOnlyList<string> All { get; } =
    [
        "ابدأ يومك بكوب ماء قبل القهوة، جسمك يحتاجه بعد ساعات النوم.",
        "الاستمرارية أهم من الكمال: يوم متوسط أفضل من يوم ضائع.",
        "أضف مصدر بروتين لكل وجبة، يساعدك على الشبع والحفاظ على العضلات.",
        "المشي عشر دقائق بعد الأكل يساعد على هضم أفضل وطاقة أثبت.",
        "سجّل وجبتك فور أكلها، الذاكرة تخدعنا في نهاية اليوم.",
        "النوم الجيد جزء من التمرين؛ العضلات تُبنى وأنت نائم.",
        "لا تعوّض الوجبة الكبيرة بالجوع، ارجع لخطتك في الوجبة التالية.",
        "اجعل نصف صحنك خضاراً، تشبع بسعرات أقل.",
        "التقدم في التمرين يأتي من زيادة صغيرة: تكرار واحد أو كيلو واحد.",
        "قِس تقدمك بالأسابيع لا بالأيام، الوزن يتذبذب يومياً وهذا طبيعي.",
        "جهّز وجبتك مسبقاً عندما تعرف أن يومك سيكون مزدحماً.",
        "العطش أحياناً يشبه الجوع، اشرب ماء وانتظر قليلاً.",
        "الإحماء خمس دقائق يقلل الإصابات ويحسّن أداءك.",
        "التمر مصدر طاقة ممتاز قبل التمرين بنصف ساعة.",
        "لا تقارن بدايتك بمنتصف رحلة غيرك.",
        "قلّل المشروبات المحلاة تدريجياً، السعرات السائلة لا تُشبع.",
        "التمرين القصير أفضل من لا شيء: عشرون دقيقة تكفي لتحافظ على العادة.",
        "اختر الدرج بدل المصعد حين تستطيع، الحركة اليومية تتراكم.",
        "الأكل ببطء يعطي جسمك وقتاً ليشعر بالشبع.",
        "خطط ليوم الراحة كما تخطط ليوم التمرين؛ الراحة جزء من التقدم.",
        "احتفل بالإنجازات الصغيرة: أسبوع التزام كامل يستحق التقدير.",
        "البروتين في الفطور يقلل الرغبة في الحلويات خلال اليوم.",
        "ضع زجاجة الماء أمامك، ما تراه تتذكره.",
        "إذا فاتك تمرين، لا تفوّت الثاني. هذه هي القاعدة الذهبية.",
        "تقنية التمرين الصحيحة أهم من الوزن الثقيل.",
        "في المناسبات، استمتع باعتدال ثم عد لروتينك في اليوم التالي.",
        "الخضار والفواكه الملونة تعطيك فيتامينات متنوعة.",
        "النوم المتأخر يزيد الشهية في اليوم التالي، حاول أن تنام مبكراً.",
        "التمدد بعد التمرين يساعد على المرونة والاسترخاء.",
        "اجعل هدفك واضحاً ومكتوباً، الهدف المكتوب أقرب للتحقيق.",
        "كل خطوة اليوم تقرّبك من هدفك. استمر.",
    ];

    /// <summary>The same tips in English, in the same order.</summary>
    public static IReadOnlyList<string> AllEnglish { get; } =
    [
        "Start your day with a glass of water before coffee; your body needs it after hours of sleep.",
        "Consistency beats perfection: an average day is better than a lost day.",
        "Add a protein source to every meal; it keeps you full and protects your muscles.",
        "A ten-minute walk after eating helps digestion and steadier energy.",
        "Log your meal right after eating it; memory plays tricks by the end of the day.",
        "Good sleep is part of training; muscles are built while you sleep.",
        "Don't make up for a big meal by starving; just get back to your plan at the next meal.",
        "Fill half your plate with vegetables to feel full on fewer calories.",
        "Progress in training comes from small steps: one more rep or one more kilo.",
        "Measure progress in weeks, not days; daily weight swings are normal.",
        "Prepare your meal ahead when you know your day will be busy.",
        "Thirst sometimes feels like hunger; drink water and wait a little.",
        "A five-minute warm-up lowers the risk of injury and improves your performance.",
        "Dates are an excellent energy source half an hour before training.",
        "Don't compare your beginning with someone else's middle.",
        "Cut sugary drinks gradually; liquid calories don't fill you up.",
        "A short workout beats none: twenty minutes is enough to keep the habit.",
        "Take the stairs instead of the lift when you can; daily movement adds up.",
        "Eating slowly gives your body time to feel full.",
        "Plan your rest day like you plan your training day; rest is part of progress.",
        "Celebrate small wins: a full week of consistency deserves credit.",
        "Protein at breakfast reduces sweet cravings later in the day.",
        "Keep your water bottle in sight; what you see, you remember.",
        "If you miss a workout, don't miss the next one. That's the golden rule.",
        "Good technique matters more than heavy weight.",
        "At gatherings, enjoy in moderation, then return to your routine the next day.",
        "Colourful vegetables and fruit give you a variety of vitamins.",
        "Late nights raise your appetite the next day; try to sleep earlier.",
        "Stretching after training helps flexibility and relaxation.",
        "Make your goal clear and written down; a written goal is closer to being reached.",
        "Every step today brings you closer to your goal. Keep going.",
    ];

    public static string For(DateOnly date)
    {
        var index = (date.DayOfYear - 1) % All.Count;
        return Lang.T(All[index], AllEnglish[index]);
    }
}
