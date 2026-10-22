using FluentValidation;
using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Validation;

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
        RuleFor(x => x.Ml).NotNull().WithMessage("الكمية مطلوبة.")
            .InclusiveBetween(50, 2000).WithMessage("الكمية بين 50 و 2000 مل.");
    }
}

public sealed class WaterReminderRequestValidator : AbstractValidator<WaterReminderRequest>
{
    public WaterReminderRequestValidator()
    {
        RuleFor(x => x.Enabled).NotNull().WithMessage("حدد تفعيل التذكير.");
        RuleFor(x => x.IntervalHours).NotNull().WithMessage("حدد مدة التذكير.")
            .InclusiveBetween(1, 6).WithMessage("مدة التذكير بين ساعة و 6 ساعات.");
    }
}
