namespace Himma.Api.Domain.Entities;

/// <summary>
/// Tape measurements and optional device readings (InBody, DEXA) for one date. The weight is not stored here:
/// it comes from the weight log, so there is one source of truth for weight.
/// </summary>
public class BodyMeasurement
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }

    public double? NeckCm { get; set; }
    public double? WaistCm { get; set; }
    public double? HipCm { get; set; }
    public double? ChestCm { get; set; }
    public double? ArmCm { get; set; }
    public double? ThighCm { get; set; }

    /// <summary>Body fat from a device; when set it is used instead of the tape-measure estimate.</summary>
    public double? BodyFatPercent { get; set; }

    /// <summary>Skeletal muscle mass from a device. Cannot be estimated from tape measurements.</summary>
    public double? MuscleMassKg { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
