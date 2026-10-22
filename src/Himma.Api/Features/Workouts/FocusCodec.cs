using Himma.Api.Domain;

namespace Himma.Api.Features.Workouts;

/// <summary>A day's focus is stored as a comma-separated list of muscle groups, e.g. "Chest,Triceps".</summary>
public static class FocusCodec
{
    public static string Encode(IEnumerable<MuscleGroup> focus) =>
        string.Join(",", focus.Distinct().Order());

    public static IReadOnlyList<MuscleGroup> Decode(string stored) =>
        stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => Enum.TryParse<MuscleGroup>(part, out var group) ? (MuscleGroup?)group : null)
            .OfType<MuscleGroup>()
            .ToList();
}
