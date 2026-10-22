using FluentValidation;
using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Validation;
using Himma.Api.Infrastructure.Localization;

namespace Himma.Api.Features.Weight;

public sealed class WeightRequest
{
    public string Date { get; init; } = string.Empty;
    public double? WeightKg { get; init; }
}

public sealed record WeightEntryDto(int Id, DateOnly Date, double WeightKg)
{
    public static WeightEntryDto From(WeightEntry w) => new(w.Id, w.Date, w.WeightKg);
}

/// <summary>Entries oldest first, plus what the progress bar needs.</summary>
public sealed record WeightHistoryResponse(
    IReadOnlyList<WeightEntryDto> Entries,
    double? StartWeightKg,
    double? LatestWeightKg,
    double? TargetWeightKg);

public sealed class WeightRequestValidator : AbstractValidator<WeightRequest>
{
    public WeightRequestValidator()
    {
        RuleFor(x => x.Date).MustBeApiDate();
        RuleFor(x => x.WeightKg).NotNull().WithMessage(_ => Lang.T("الوزن مطلوب.", "Weight is required."))
            .InclusiveBetween(30, 300).WithMessage(_ => Lang.T("الوزن بين 30 و 300 كغ.", "Weight must be between 30 and 300 kg."));
    }
}
