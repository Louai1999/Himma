namespace Himma.Api.Domain.Training;

/// <summary>One completed set from an earlier session.</summary>
public sealed record PastSet(int Reps, double? WeightKg, int? Rir);

/// <summary>
/// Kind: "increase" (add weight), "addReps" (bodyweight: add reps) or "keep" (stay at this weight).
/// Reason says why: "twoSessionsHit", "easyLastTime" or "missedReps".
/// </summary>
public sealed record ProgressionHint(string Kind, string Reason, double? WeightKg, int? Reps);

public static class ProgressionKinds
{
    public const string Increase = "increase";
    public const string AddReps = "addReps";
    public const string Keep = "keep";
}

public static class ProgressionReasons
{
    public const string TwoSessionsHit = "twoSessionsHit";
    public const string EasyLastTime = "easyLastTime";
    public const string MissedReps = "missedReps";
}

/// <summary>
/// Progressive overload: once every planned set reaches the planned reps at the same weight two sessions in
/// a row (or once with 3+ reps to spare), it is time to go heavier. Missing reps by a lot means stay.
/// </summary>
public static class Progression
{
    public const int EasyRir = 3;

    /// <param name="sessions">Earlier sessions with this exercise, newest first, each with its completed sets.</param>
    public static ProgressionHint? Advise(MuscleGroup muscle, int plannedSets, int plannedReps, IReadOnlyList<IReadOnlyList<PastSet>> sessions)
    {
        if (sessions.Count == 0 || sessions[0].Count == 0 || plannedSets < 1 || plannedReps < 1)
        {
            return null;
        }

        var last = sessions[0];
        var top = last.Max(s => s.WeightKg ?? 0);
        var bodyweight = top <= 0;

        if (Hit(last, top, plannedSets, plannedReps))
        {
            var topSets = last.Where(s => (s.WeightKg ?? 0) == top).ToList();
            var easy = topSets.All(s => s.Rir is >= EasyRir);
            var twice = sessions.Count > 1 && sessions[1].Count > 0
                && sessions[1].Max(s => s.WeightKg ?? 0) == top
                && Hit(sessions[1], top, plannedSets, plannedReps);
            if (!easy && !twice)
            {
                return null;
            }

            var reason = twice ? ProgressionReasons.TwoSessionsHit : ProgressionReasons.EasyLastTime;
            return bodyweight
                ? new ProgressionHint(ProgressionKinds.AddReps, reason, null, plannedReps + 2)
                : new ProgressionHint(ProgressionKinds.Increase, reason, top + Step(muscle, top), plannedReps);
        }

        // Two or more reps short on a set: the weight is still heavy enough.
        var missedByMuch = last.Where(s => (s.WeightKg ?? 0) == top).Any(s => s.Reps <= plannedReps - 2);
        return missedByMuch && !bodyweight
            ? new ProgressionHint(ProgressionKinds.Keep, ProgressionReasons.MissedReps, top, plannedReps)
            : null;
    }

    /// <summary>Every planned set at the top weight reached the planned reps, without going to failure.</summary>
    private static bool Hit(IReadOnlyList<PastSet> sets, double top, int plannedSets, int plannedReps)
    {
        var topSets = sets.Where(s => (s.WeightKg ?? 0) == top).ToList();
        return topSets.Count >= plannedSets
            && topSets.All(s => s.Reps >= plannedReps)
            && topSets.All(s => s.Rir is not 0);
    }

    /// <summary>Legs move more weight, so they go up in bigger steps; very light weights in small ones.</summary>
    public static double Step(MuscleGroup muscle, double currentKg) =>
        currentKg < 10 ? 1 : muscle == MuscleGroup.Legs ? 5 : 2.5;
}
