using FluentValidation;
using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Validation;
using Himma.Api.Infrastructure.Localization;

namespace Himma.Api.Features.Water;

public sealed class WaterRequest
{
    public string Date { get; init; } = string.Empty;
    public int? Ml { get; init; }
}

public sealed class WaterReminderRequest
{
    public bool? Enabled { get; init; }
    public int? IntervalHours { get; init; }
}

public sealed record WaterEntryDto(int Id, DateOnly Date, int Ml, DateTime CreatedAtUtc)
{
    public static WaterEntryDto From(WaterEntry w) => new(w.Id, w.Date, w.Ml, w.CreatedAtUtc);
}

public sealed record WaterDayResponse(DateOnly Date, int TotalMl, IReadOnlyList<WaterEntryDto> Entries);

public sealed record WaterReminderDto(bool Enabled, int IntervalHours);

public sealed class WaterRequestValidator : AbstractValidator<WaterRequest>
{
    public WaterRequestValidator()
    {
        RuleFor(x => x.Date).MustBeApiDate();
        RuleFor(x => x.Ml).NotNull().WithMessage(_ => Lang.T("الكمية مطلوبة.", "Amount is required."))
            .InclusiveBetween(50, 2000).WithMessage(_ => Lang.T("الكمية بين 50 و 2000 مل.", "Amount must be between 50 and 2000 ml."));
    }
}

public sealed class WaterReminderRequestValidator : AbstractValidator<WaterReminderRequest>
{
    public WaterReminderRequestValidator()
    {
        RuleFor(x => x.Enabled).NotNull().WithMessage(_ => Lang.T("حدد تفعيل التذكير.", "Choose whether the reminder is on."));
        RuleFor(x => x.IntervalHours).NotNull().WithMessage(_ => Lang.T("حدد مدة التذكير.", "Choose how often to remind you."))
            .InclusiveBetween(1, 6).WithMessage(_ => Lang.T("مدة التذكير بين ساعة و 6 ساعات.", "The reminder interval must be between 1 and 6 hours."));
    }
}
