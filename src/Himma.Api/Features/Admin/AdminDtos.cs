using FluentValidation;
using Himma.Api.Domain;
using Himma.Api.Domain.Nutrition;
using Himma.Api.Features.Account;
using Himma.Api.Features.History;
using Himma.Api.Features.Profile;
using Himma.Api.Infrastructure.Localization;
using Himma.Api.Infrastructure.Validation;

namespace Himma.Api.Features.Admin;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record WeeklyCount(DateOnly WeekStart, int Count);

public sealed record AdminOverview(
    int TotalUsers,
    int NewLast7Days,
    int NewLast30Days,
    int ActiveLast7Days,
    int Suspended,
    int PendingDeletion,
    double? AvgCalorieAdherencePercent,
    double? AvgProteinAdherencePercent,
    double? AvgWaterAdherencePercent,
    double? AvgConsistencyPercent,
    double? RetentionPercent,
    IReadOnlyList<WeeklyCount> WeeklySignups);

public sealed record AdminUserRow(
    string Id,
    string Username,
    string DisplayName,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc,
    DateTime? LastActivityAtUtc,
    AccountStatus Status,
    Goal? Goal,
    int Streak,
    double ConsistencyPercent,
    double? CalorieAdherencePercent);

public sealed record WorkoutCompliance(
    string? ActivePlanName,
    int TrainingDaysPerWeek,
    int CompletedSessions30,
    int PlannedSessions30,
    double? CompliancePercent);

public sealed record AdminUserDetail(
    string Id,
    string Username,
    string DisplayName,
    AccountStatus Status,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc,
    DateTime? LastActivityAtUtc,
    DateTime? DeletedAtUtc,
    DateTime? DeletionScheduledForUtc,
    ProfileDto? Profile,
    DailyTargets? Targets,
    int Streak,
    AdherenceSummary Adherence30,
    IReadOnlyList<WeightPointDto> Weights30,
    double? WeightChange30Kg,
    IReadOnlyList<SummaryDayDto> Last14Days,
    WorkoutCompliance Workouts,
    bool HasRecoveryEmail,
    bool MustChangePassword);

public sealed record AuditLogRow(
    long Id,
    DateTime CreatedAtUtc,
    string ActorUserId,
    string? ActorUsername,
    string ActorRole,
    string Action,
    string? TargetUserId,
    string? TargetUsername,
    string Details,
    string? IpAddress);

// ---------- Query parameters ----------

public static class AdminUserStatusFilter
{
    /// <summary>Default list: active and suspended accounts (pending deletion is hidden).</summary>
    public const string Current = "";
    public const string Active = "active";
    public const string Suspended = "suspended";
    public const string PendingDeletion = "pendingDeletion";
    public const string All = "all";

    public static readonly string[] Values = [Current, Active, Suspended, PendingDeletion, All];
}

public static class AdminUserSort
{
    public const string JoinedDesc = "joined_desc";
    public const string JoinedAsc = "joined_asc";
    public const string LastLoginDesc = "lastLogin_desc";
    public const string UsernameAsc = "username_asc";
    public const string StreakDesc = "streak_desc";
    public const string ConsistencyDesc = "consistency_desc";
    public const string AdherenceDesc = "adherence_desc";

    public static readonly string[] Values = [JoinedDesc, JoinedAsc, LastLoginDesc, UsernameAsc, StreakDesc, ConsistencyDesc, AdherenceDesc];

    /// <summary>Sorts that need the computed metrics of every matching user.</summary>
    public static bool IsMetric(string sort) => sort is StreakDesc or ConsistencyDesc or AdherenceDesc;
}

public sealed class AdminUserQuery
{
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? JoinedFrom { get; init; }
    public string? JoinedTo { get; init; }
    public string? Sort { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed class AuditLogQuery
{
    /// <summary>User id or username.</summary>
    public string? Actor { get; init; }

    /// <summary>User id or username.</summary>
    public string? Target { get; init; }

    public string? Action { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed class AdminUserQueryValidator : AbstractValidator<AdminUserQuery>
{
    public AdminUserQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(60).WithMessage(_ => Lang.T("نص البحث طويل جداً.", "The search text is too long."));
        RuleFor(x => x.Status ?? string.Empty).Must(s => AdminUserStatusFilter.Values.Contains(s))
            .OverridePropertyName("status").WithMessage(_ => Lang.T("حالة غير معروفة.", "Unknown status."));
        RuleFor(x => x.Sort ?? AdminUserSort.JoinedDesc).Must(s => AdminUserSort.Values.Contains(s))
            .OverridePropertyName("sort").WithMessage(_ => Lang.T("ترتيب غير معروف.", "Unknown sort order."));
        RuleFor(x => x.JoinedFrom!).MustBeApiDate().When(x => !string.IsNullOrEmpty(x.JoinedFrom));
        RuleFor(x => x.JoinedTo!).MustBeApiDate().When(x => !string.IsNullOrEmpty(x.JoinedTo));
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class AuditLogQueryValidator : AbstractValidator<AuditLogQuery>
{
    public AuditLogQueryValidator()
    {
        RuleFor(x => x.Actor).MaximumLength(64);
        RuleFor(x => x.Target).MaximumLength(64);
        RuleFor(x => x.Action).MaximumLength(64);
        RuleFor(x => x.From!).MustBeApiDate().When(x => !string.IsNullOrEmpty(x.From));
        RuleFor(x => x.To!).MustBeApiDate().When(x => !string.IsNullOrEmpty(x.To));
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

// ---------- Actions ----------

/// <summary>Destructive admin actions ask for the admin's own password again.</summary>
/// <summary>Shown to the admin once; the user must replace it at the next sign-in.</summary>
public sealed record TemporaryPasswordResponse(string TemporaryPassword);

public sealed class AdminReauthRequest
{
    public string AdminPassword { get; init; } = string.Empty;
}

public sealed class PurgeRequest
{
    public string AdminPassword { get; init; } = string.Empty;

    /// <summary>Must equal the target's username exactly, so the right account is erased.</summary>
    public string ConfirmUsername { get; init; } = string.Empty;
}

public sealed class AdminReauthRequestValidator : AbstractValidator<AdminReauthRequest>
{
    public AdminReauthRequestValidator()
    {
        RuleFor(x => x.AdminPassword).NotEmpty().WithMessage(_ => Lang.T("أدخل كلمة مرورك للتأكيد.", "Enter your password to confirm.")).MaximumLength(128);
    }
}

public sealed class PurgeRequestValidator : AbstractValidator<PurgeRequest>
{
    public PurgeRequestValidator()
    {
        RuleFor(x => x.AdminPassword).NotEmpty().WithMessage(_ => Lang.T("أدخل كلمة مرورك للتأكيد.", "Enter your password to confirm.")).MaximumLength(128);
        RuleFor(x => x.ConfirmUsername).NotEmpty().WithMessage(_ => Lang.T("اكتب اسم المستخدم للتأكيد.", "Type the username to confirm.")).MaximumLength(30);
    }
}
