using System.Globalization;
using FluentValidation;
using Himma.Api.Infrastructure.Localization;

namespace Himma.Api.Infrastructure.Validation;

/// <summary>
/// Dates are "yyyy-MM-dd" strings in the user's local timezone, chosen by the client.
/// The server only checks the format and that the date is not in the future.
/// </summary>
public static class ApiDate
{
    public const string Format = "yyyy-MM-dd";
    public static readonly DateOnly Earliest = new(2000, 1, 1);

    public static DateOnly UtcToday => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>A user's local date can be up to 14 hours ahead of UTC, so allow tomorrow (UTC).</summary>
    public static DateOnly Latest => UtcToday.AddDays(1);

    public static bool TryParse(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
        && date >= Earliest
        && date <= Latest;

    public static DateOnly Parse(string value) =>
        TryParse(value, out var date) ? date : throw new FormatException($"'{value}' is not a valid API date.");

    /// <summary>True when the value can be "today" somewhere on Earth right now.</summary>
    public static bool IsPlausibleToday(DateOnly date) =>
        date >= UtcToday.AddDays(-1) && date <= UtcToday.AddDays(1);

    public static string InvalidMessage => Lang.T(
        "التاريخ يجب أن يكون بالصيغة yyyy-MM-dd وألا يكون في المستقبل.",
        "The date must be in yyyy-MM-dd format and not in the future.");

    public static IRuleBuilderOptions<T, string> MustBeApiDate<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(value => TryParse(value, out _)).WithMessage(_ => InvalidMessage);
}
