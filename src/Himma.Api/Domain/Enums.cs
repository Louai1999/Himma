namespace Himma.Api.Domain;

// Enums are stored as strings in the database and serialized as camelCase strings in JSON,
// so renaming a member is a breaking change. Add new members at the end.

public enum Gender { Male, Female }

/// <summary>Multipliers: Sedentary 1.2, Light 1.375, Moderate 1.55, Active 1.725, VeryActive 1.9.</summary>
public enum ActivityLevel { Sedentary, Light, Moderate, Active, VeryActive }

public enum Goal { Lose, Maintain, Gain }

public enum MealType { Breakfast, Lunch, Dinner, Snack }

public enum MealSource { Manual, Custom, Ai }

public enum MuscleGroup { Chest, Triceps, Back, Biceps, Legs, Shoulders, Abs, FullBody, Cardio }

public enum BodyRegion { Upper, Middle, Lower, Whole }

public enum WorkoutPlace { Home, Gym }

/// <summary>Week starts on Saturday, as is common in the Arab world.</summary>
public enum DayKey { Sat, Sun, Mon, Tue, Wed, Thu, Fri }

public enum CoachRole { User, Assistant }
