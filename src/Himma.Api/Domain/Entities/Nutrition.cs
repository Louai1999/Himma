namespace Himma.Api.Domain.Entities;

public class UserProfile
{
    /// <summary>One profile per user, so the user id is the primary key.</summary>
    public string UserId { get; set; } = string.Empty;

    public Gender Gender { get; set; }
    public int Age { get; set; }
    public double HeightCm { get; set; }
    public double WeightKg { get; set; }
    public ActivityLevel ActivityLevel { get; set; }
    public Goal Goal { get; set; }
    public double? TargetWeightKg { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>System food from the seeded database (read-only for users).</summary>
public class Food
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double KcalPer100g { get; set; }
    public double ProteinPer100g { get; set; }
    public double CarbsPer100g { get; set; }
    public double FatPer100g { get; set; }
    public double? ServingGrams { get; set; }
    public string? ServingLabel { get; set; }
}

public class CustomFood
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double KcalPer100g { get; set; }
    public double ProteinPer100g { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class MealEntry
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public MealType MealType { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Grams { get; set; }
    public double Calories { get; set; }
    public double ProteinG { get; set; }
    public double CarbsG { get; set; }
    public double FatG { get; set; }
    public MealSource Source { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class WaterEntry
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public int Ml { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class WeightEntry
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public double WeightKg { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class WaterReminderSetting
{
    public string UserId { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int IntervalHours { get; set; } = 2;
}
