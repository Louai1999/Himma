using FluentValidation;
using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Domain.Nutrition;
using Himma.Api.Features.Admin;
using Himma.Api.Features.History;
using Himma.Api.Features.Profile;
using Himma.Api.Infrastructure.Localization;
using Himma.Api.Infrastructure.Validation;

namespace Himma.Api.Features.Coaching;

// ---------- Coach side ----------

public sealed record CoachAccountDto(
    string BusinessName,
    string? Bio,
    string InviteCode,
    int MaxClients,
    int ActiveClients,
    DateTime ActiveUntilUtc,
    bool IsActive)
{
    public static CoachAccountDto From(CoachAccount a, int activeClients) =>
        new(a.BusinessName, a.Bio, a.InviteCode, a.MaxClients, activeClients, a.ActiveUntilUtc, CoachingService.IsActive(a));
}

/// <summary>
/// Reasons a client needs the coach's attention, worst first:
/// inactive (no meal, water, weight or workout logged for 3+ days), noProfile, lowCalories, lowProtein, lowWater
/// (on target on under half of the last 7 days).
/// </summary>
public static class AttentionReasons
{
    public const string Inactive = "inactive";
    public const string NoProfile = "noProfile";
    public const string LowCalories = "lowCalories";
    public const string LowProtein = "lowProtein";
    public const string LowWater = "lowWater";
}

public sealed record CoachClientRow(
    string Id,
    string Username,
    string DisplayName,
    DateTime JoinedAtUtc,
    Goal? Goal,
    int Streak,
    double ConsistencyPercent,
    double? CalorieAdherencePercent,
    double? ProteinAdherencePercent,
    double? WaterAdherencePercent,
    DateTime? LastLoggedAtUtc,
    double? WeightChange30Kg,
    DateTime? LastMessageAtUtc,
    IReadOnlyList<string> Attention);

public sealed record CoachClientsResponse(CoachAccountDto Coach, IReadOnlyList<CoachClientRow> Clients);

public sealed record CoachNoteDto(int Id, string Text, DateTime CreatedAtUtc, DateTime? ReadAtUtc)
{
    public static CoachNoteDto From(CoachNote n) => new(n.Id, n.Text, n.CreatedAtUtc, n.ReadAtUtc);
}

public sealed record CoachClientDetail(
    string Id,
    string Username,
    string DisplayName,
    DateTime JoinedAtUtc,
    string? PrivateNote,
    ProfileDto? Profile,
    DailyTargets? Targets,
    int Streak,
    AdherenceSummary Adherence30,
    IReadOnlyList<WeightPointDto> Weights30,
    double? WeightChange30Kg,
    IReadOnlyList<SummaryDayDto> Last14Days,
    WorkoutCompliance Workouts,
    IReadOnlyList<string> Attention,
    IReadOnlyList<CoachNoteDto> Messages);

public sealed class CoachProfileRequest
{
    public string BusinessName { get; init; } = string.Empty;
    public string? Bio { get; init; }
}

public sealed class CoachMessageRequest
{
    public string Text { get; init; } = string.Empty;
}

public sealed class CoachPrivateNoteRequest
{
    public string? Note { get; init; }
}

public sealed class AssignPlanRequest
{
    public int PlanId { get; init; }

    /// <summary>True makes the copy the client's active plan right away.</summary>
    public bool Activate { get; init; } = true;
}

public sealed record AssignPlanResponse(int PlanId, string Name, bool IsActive);

// ---------- Client side ----------

public sealed record CoachPublicDto(string BusinessName, string? Bio, string CoachName);

public sealed record MyCoachResponse(
    CoachPublicDto? Coach,
    DateTime? JoinedAtUtc,
    IReadOnlyList<CoachNoteDto> Messages,
    int UnreadCount);

public sealed class JoinCoachRequest
{
    public string Code { get; init; } = string.Empty;
}

// ---------- Admin side ----------

public sealed class AdminCoachRequest
{
    public string BusinessName { get; init; } = string.Empty;
    public int MaxClients { get; init; }

    /// <summary>yyyy-MM-dd; the package works until the end of that day (UTC).</summary>
    public string ActiveUntil { get; init; } = string.Empty;
}

public sealed record AdminCoachRow(
    string UserId,
    string Username,
    string DisplayName,
    string BusinessName,
    string InviteCode,
    int MaxClients,
    int ActiveClients,
    DateTime ActiveUntilUtc,
    bool IsActive,
    DateTime CreatedAtUtc);

// ---------- Validation ----------

public sealed class CoachProfileRequestValidator : AbstractValidator<CoachProfileRequest>
{
    public CoachProfileRequestValidator()
    {
        RuleFor(x => x.BusinessName).Cascade(CascadeMode.Stop)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage(_ => Lang.T("اكتب اسم النشاط أو اسمك كمدرب.", "Enter your coaching or gym name."))
            .Must(v => v.Trim().Length <= 60).WithMessage(_ => Lang.T("الاسم طويل جداً (60 حرفاً كحد أقصى).", "The name is too long (60 characters at most)."));
        RuleFor(x => x.Bio)
            .Must(v => v is null || v.Trim().Length <= 300).WithMessage(_ => Lang.T("النبذة طويلة جداً (300 حرف كحد أقصى).", "The bio is too long (300 characters at most)."));
    }
}

public sealed class CoachMessageRequestValidator : AbstractValidator<CoachMessageRequest>
{
    public CoachMessageRequestValidator()
    {
        RuleFor(x => x.Text).Cascade(CascadeMode.Stop)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage(_ => Lang.T("اكتب الرسالة.", "Write the message."))
            .Must(v => v.Trim().Length <= 500).WithMessage(_ => Lang.T("الرسالة طويلة جداً (500 حرف كحد أقصى).", "The message is too long (500 characters at most)."));
    }
}

public sealed class CoachPrivateNoteRequestValidator : AbstractValidator<CoachPrivateNoteRequest>
{
    public CoachPrivateNoteRequestValidator()
    {
        RuleFor(x => x.Note)
            .Must(v => v is null || v.Trim().Length <= 2000).WithMessage(_ => Lang.T("الملاحظة طويلة جداً (2000 حرف كحد أقصى).", "The note is too long (2000 characters at most)."));
    }
}

public sealed class AssignPlanRequestValidator : AbstractValidator<AssignPlanRequest>
{
    public AssignPlanRequestValidator()
    {
        RuleFor(x => x.PlanId).GreaterThan(0).WithMessage(_ => Lang.T("اختر خطة.", "Choose a plan."));
    }
}

public sealed class JoinCoachRequestValidator : AbstractValidator<JoinCoachRequest>
{
    public JoinCoachRequestValidator()
    {
        RuleFor(x => x.Code)
            .Must(v => CoachingService.NormalizeCode(v).Length is >= 4 and <= 16)
            .WithMessage(_ => Lang.T("اكتب كود المدرب كما وصلك.", "Enter the coach code exactly as you received it."));
    }
}

public sealed class AdminCoachRequestValidator : AbstractValidator<AdminCoachRequest>
{
    public const int MaxSeats = 500;

    public AdminCoachRequestValidator()
    {
        RuleFor(x => x.BusinessName).Cascade(CascadeMode.Stop)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage(_ => Lang.T("اكتب اسم المدرب أو النادي.", "Enter the coach or gym name."))
            .Must(v => v.Trim().Length <= 60).WithMessage(_ => Lang.T("الاسم طويل جداً (60 حرفاً كحد أقصى).", "The name is too long (60 characters at most)."));
        RuleFor(x => x.MaxClients).InclusiveBetween(1, MaxSeats)
            .WithMessage(_ => Lang.T("عدد المتدربين بين 1 و 500.", "Clients must be between 1 and 500."));
        RuleFor(x => x.ActiveUntil)
            .Must(v => TryParseUntil(v, out _))
            .WithMessage(_ => Lang.T("تاريخ الانتهاء يجب أن يكون بالصيغة yyyy-MM-dd وخلال 5 سنوات.", "The end date must be in yyyy-MM-dd format and within 5 years."));
    }

    /// <summary>An end date may be in the past (to end a package) but not more than 5 years ahead.</summary>
    public static bool TryParseUntil(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value, ApiDate.Format, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date)
        && date >= ApiDate.Earliest
        && date <= ApiDate.UtcToday.AddYears(5);
}
