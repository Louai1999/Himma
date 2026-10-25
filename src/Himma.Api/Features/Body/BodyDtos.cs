using FluentValidation;
using Himma.Api.Domain.Body;
using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Localization;
using Himma.Api.Infrastructure.Validation;

namespace Himma.Api.Features.Body;

/// <summary>Every measurement is optional, but at least one must be given.</summary>
public sealed class BodyMeasurementRequest
{
    public string Date { get; init; } = string.Empty;
    public double? NeckCm { get; init; }
    public double? WaistCm { get; init; }
    public double? HipCm { get; init; }
    public double? ChestCm { get; init; }
    public double? ArmCm { get; init; }
    public double? ThighCm { get; init; }
    public double? BodyFatPercent { get; init; }
    public double? MuscleMassKg { get; init; }

    public bool HasAnyValue =>
        NeckCm is not null || WaistCm is not null || HipCm is not null || ChestCm is not null
        || ArmCm is not null || ThighCm is not null || BodyFatPercent is not null || MuscleMassKg is not null;
}

public sealed class BodyGoalRequest
{
    /// <summary>Null clears the goal.</summary>
    public double? TargetBodyFatPercent { get; init; }
}

public sealed record BodyEntryDto(
    int Id,
    DateOnly Date,
    double? NeckCm,
    double? WaistCm,
    double? HipCm,
    double? ChestCm,
    double? ArmCm,
    double? ThighCm,
    double? BodyFatPercentInput,
    double? MuscleMassKg,
    double? WeightKg,
    BodyComposition? Composition)
{
    public static BodyEntryDto From(BodyMeasurement m, double? weightKg, BodyComposition? composition) =>
        new(m.Id, m.Date, m.NeckCm, m.WaistCm, m.HipCm, m.ChestCm, m.ArmCm, m.ThighCm,
            m.BodyFatPercent, m.MuscleMassKg, weightKg, composition);
}

/// <summary>Entries oldest first. Latest is the newest entry that has a body fat result.</summary>
public sealed record BodyResponse(
    bool HasProfile,
    IReadOnlyList<BodyEntryDto> Entries,
    BodyEntryDto? Latest,
    double? TargetBodyFatPercent,
    BodyFatProjection? Projection);

public sealed class BodyMeasurementRequestValidator : AbstractValidator<BodyMeasurementRequest>
{
    public BodyMeasurementRequestValidator()
    {
        RuleFor(x => x.Date).MustBeApiDate();
        RuleFor(x => x).Must(x => x.HasAnyValue).OverridePropertyName("body")
            .WithMessage(_ => Lang.T("أدخل قياساً واحداً على الأقل.", "Enter at least one measurement."));

        CentimetreRange(x => x.NeckCm, 20, 70, "محيط الرقبة", "Neck");
        CentimetreRange(x => x.WaistCm, 40, 200, "محيط الخصر", "Waist");
        CentimetreRange(x => x.HipCm, 50, 200, "محيط الورك", "Hips");
        CentimetreRange(x => x.ChestCm, 50, 200, "محيط الصدر", "Chest");
        CentimetreRange(x => x.ArmCm, 15, 80, "محيط الذراع", "Arm");
        CentimetreRange(x => x.ThighCm, 30, 120, "محيط الفخذ", "Thigh");

        RuleFor(x => x.BodyFatPercent)
            .InclusiveBetween(2, 70).WithMessage(_ => Lang.T("نسبة الدهون بين 2 و 70٪.", "Body fat must be between 2 and 70%."))
            .When(x => x.BodyFatPercent is not null);
        RuleFor(x => x.MuscleMassKg)
            .InclusiveBetween(10, 150).WithMessage(_ => Lang.T("الكتلة العضلية بين 10 و 150 كغ.", "Muscle mass must be between 10 and 150 kg."))
            .When(x => x.MuscleMassKg is not null);
    }

    /// <summary>Optional value in centimetres; checked only when given.</summary>
    private void CentimetreRange(System.Linq.Expressions.Expression<Func<BodyMeasurementRequest, double?>> field,
        double min, double max, string arabic, string english)
    {
        RuleFor(field)
            .InclusiveBetween(min, max)
            .WithMessage(_ => Lang.T($"{arabic} بين {min} و {max} سم.", $"{english} must be between {min} and {max} cm."));
    }
}

public sealed class BodyGoalRequestValidator : AbstractValidator<BodyGoalRequest>
{
    public BodyGoalRequestValidator()
    {
        RuleFor(x => x.TargetBodyFatPercent)
            .InclusiveBetween(4, 50).WithMessage(_ => Lang.T("نسبة الدهون المستهدفة بين 4 و 50٪.", "Target body fat must be between 4 and 50%."))
            .When(x => x.TargetBodyFatPercent is not null);
    }
}
