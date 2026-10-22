using FluentValidation;
using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Validation;

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
        RuleFor(x => x.WeightKg).NotNull().WithMessage("الوزن مطلوب.")
            .InclusiveBetween(30, 300).WithMessage("الوزن بين 30 و 300 كغ.");
    }
}
