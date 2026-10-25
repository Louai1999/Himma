using System.Globalization;
using FluentValidation;
using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Domain.Supplements;
using Himma.Api.Infrastructure.Localization;
using Himma.Api.Infrastructure.Validation;

namespace Himma.Api.Features.Supplements;

public sealed class SupplementRequest
{
    public string Name { get; init; } = string.Empty;
    public double? Dose { get; init; }
    public SupplementUnit? Unit { get; init; }

    /// <summary>Reminder times as "HH:mm". Empty means no fixed time (one dose a day, no reminder).</summary>
    public List<string> Times { get; init; } = [];

    public List<DayKey> Days { get; init; } = [];
    public double? ProteinG { get; init; }
    public double? Calories { get; init; }
    public string? Notes { get; init; }
}

public sealed class IntakeRequest
{
    public string Date { get; init; } = string.Empty;

    /// <summary>Log another dose even though today's doses are already logged.</summary>
    public bool Force { get; init; }
}

/// <summary>Sent by the service worker when "Taken" is tapped on a notification.</summary>
public sealed class TakeFromNotificationRequest
{
    public string Token { get; init; } = string.Empty;
}

public sealed record SupplementDto(
    int Id,
    string Name,
    double Dose,
    SupplementUnit Unit,
    IReadOnlyList<string> Times,
    IReadOnlyList<DayKey> Days,
    double? ProteinG,
    double? Calories,
    string? Notes)
{
    public static SupplementDto From(Supplement s) =>
        new(s.Id, s.Name, s.Dose, s.Unit,
            SupplementSchedule.ParseTimes(s.Times).Select(t => t.ToString("HH:mm", CultureInfo.InvariantCulture)).ToList(),
            SupplementSchedule.FromMask(s.DaysMask), s.ProteinG, s.Calories, s.Notes);
}

public sealed record IntakeDto(int Id, int SupplementId, DateOnly Date, DateTime TakenAtUtc)
{
    public static IntakeDto From(SupplementIntake i) => new(i.Id, i.SupplementId, i.Date, i.TakenAtUtc);
}

/// <summary>DosesDue is 0 on days off the schedule. Streak counts complete scheduled days in a row.</summary>
public sealed record SupplementDayItem(
    SupplementDto Supplement,
    bool Scheduled,
    int DosesDue,
    IReadOnlyList<IntakeDto> Intakes,
    int Streak);

public sealed record SupplementDayResponse(DateOnly Date, IReadOnlyList<SupplementDayItem> Items);

public sealed record TakeFromNotificationResponse(bool AlreadyTaken, string SupplementName);

public sealed class SupplementRequestValidator : AbstractValidator<SupplementRequest>
{
    public SupplementRequestValidator()
    {
        RuleFor(x => x.Name).Must(n => !string.IsNullOrWhiteSpace(n))
            .WithMessage(_ => Lang.T("اسم المكمل مطلوب.", "The supplement name is required."))
            .MaximumLength(60).WithMessage(_ => Lang.T("الاسم 60 حرفاً على الأكثر.", "The name can be at most 60 characters."));
        RuleFor(x => x.Dose).NotNull().WithMessage(_ => Lang.T("الجرعة مطلوبة.", "The dose is required."))
            .InclusiveBetween(0.01, 5000).WithMessage(_ => Lang.T("الجرعة بين 0.01 و 5000.", "The dose must be between 0.01 and 5000."));
        RuleFor(x => x.Unit).NotNull().WithMessage(_ => Lang.T("اختر الوحدة.", "Choose a unit.")).IsInEnum();

        RuleFor(x => x.Times).Must(t => t.Count <= SupplementSchedule.MaxTimesPerDay)
            .WithMessage(_ => Lang.T("6 أوقات في اليوم على الأكثر.", "At most 6 times a day."));
        RuleForEach(x => x.Times).Must(BeTime)
            .WithMessage(_ => Lang.T("الوقت بصيغة 24 ساعة مثل 08:30.", "Use 24-hour time such as 08:30."));
        RuleFor(x => x.Times).Must(t => t.Distinct().Count() == t.Count)
            .WithMessage(_ => Lang.T("لا تكرر نفس الوقت.", "Do not repeat the same time."));

        RuleFor(x => x.Days).NotEmpty().WithMessage(_ => Lang.T("اختر يوماً واحداً على الأقل.", "Choose at least one day."))
            .Must(d => d.Distinct().Count() == d.Count).WithMessage(_ => Lang.T("لا تكرر نفس اليوم.", "Do not repeat a day."));
        RuleForEach(x => x.Days).IsInEnum();

        RuleFor(x => x.ProteinG).InclusiveBetween(0, 200)
            .WithMessage(_ => Lang.T("البروتين بين 0 و 200 غ.", "Protein must be between 0 and 200 g."));
        RuleFor(x => x.Calories).InclusiveBetween(0, 2000)
            .WithMessage(_ => Lang.T("السعرات بين 0 و 2000.", "Calories must be between 0 and 2000."));
        RuleFor(x => x.Notes).MaximumLength(120)
            .WithMessage(_ => Lang.T("الملاحظة 120 حرفاً على الأكثر.", "Notes can be at most 120 characters."));
    }

    private static bool BeTime(string value) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}

public sealed class IntakeRequestValidator : AbstractValidator<IntakeRequest>
{
    public IntakeRequestValidator()
    {
        RuleFor(x => x.Date).MustBeApiDate();
    }
}

public sealed class TakeFromNotificationRequestValidator : AbstractValidator<TakeFromNotificationRequest>
{
    public TakeFromNotificationRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(1000);
    }
}
