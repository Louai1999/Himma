namespace Himma.Api.Domain.Entities;

/// <summary>A supplement the user takes, with the dose they chose. Himma never suggests doses.</summary>
public class Supplement
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Dose { get; set; }
    public SupplementUnit Unit { get; set; }

    /// <summary>Reminder times as "HH:mm" separated by commas, earliest first. Empty means "no fixed time".</summary>
    public string Times { get; set; } = string.Empty;

    /// <summary>One bit per <see cref="DayKey"/> (bit 0 = Saturday). 127 = every day.</summary>
    public int DaysMask { get; set; } = 127;

    /// <summary>Optional nutrition per dose; when set, taking a dose also adds it to the day's meals.</summary>
    public double? ProteinG { get; set; }
    public double? Calories { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>One dose taken. The date is the user's local date.</summary>
public class SupplementIntake
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int SupplementId { get; set; }
    public DateOnly Date { get; set; }
    public DateTime TakenAtUtc { get; set; }

    /// <summary>The meal entry added for this dose (protein powder and the like), removed with it.</summary>
    public int? MealEntryId { get; set; }
}

/// <summary>A browser or phone that accepted push notifications for this user.</summary>
public class PushDevice
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;

    /// <summary>IANA time zone of the device, so reminders fire at the user's local time.</summary>
    public string TimeZone { get; set; } = "UTC";

    /// <summary>"ar" or "en": the language of the notifications sent to this device.</summary>
    public string Language { get; set; } = "ar";

    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>Small server-wide values the app creates itself, such as the push signing keys.</summary>
public class AppSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
