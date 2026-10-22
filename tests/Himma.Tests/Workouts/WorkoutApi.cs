using System.Net.Http.Json;
using Himma.Api.Features.Workouts;

namespace Himma.Tests.Workouts;

internal static class WorkoutApi
{
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }

    public static async Task<List<ExerciseDto>> ExercisesAsync(HttpClient client, string query = "") =>
        (await client.GetFromJsonAsync<List<ExerciseDto>>("/api/exercises" + query, TestJson.Options))!;

    public static async Task<ExerciseDto> SystemExerciseAsync(HttpClient client, string name) =>
        (await ExercisesAsync(client)).Single(e => e.IsSystem && e.Name == name);

    public static Task<HttpResponseMessage> CreateExerciseAsync(HttpClient client, string name = "تمريني الخاص", int sets = 3) =>
        client.PostAsJsonAsync("/api/exercises", new
        {
            name,
            muscleGroup = "chest",
            region = "upper",
            place = "home",
            equipment = "دمبل",
            defaultSets = sets,
            defaultReps = 10,
        });

    public static Task<HttpResponseMessage> CreatePlanAsync(HttpClient client, string name = "خطتي", string place = "gym") =>
        client.PostAsJsonAsync("/api/plans", new { name, place });

    public static Task<HttpResponseMessage> ApplyTemplateAsync(HttpClient client, string key = WorkoutTemplates.GymSplit4) =>
        client.PostAsync($"/api/plans/from-template/{key}", null);

    public static Task<HttpResponseMessage> PutDayAsync(HttpClient client, int planId, string dayKey, object body) =>
        client.PutAsJsonAsync($"/api/plans/{planId}/days/{dayKey}", body);

    public static object TrainingDay(params (int ExerciseId, int Sets, int Reps, double? WeightKg)[] items) => new
    {
        isRest = false,
        focus = new[] { "chest" },
        items = items.Select(i => new { exerciseId = i.ExerciseId, sets = i.Sets, reps = i.Reps, weightKg = i.WeightKg, restSeconds = 60 }).ToArray(),
    };

    public static Task<HttpResponseMessage> StartSessionAsync(HttpClient client, string date, int? planDayId) =>
        client.PostAsJsonAsync("/api/workouts/sessions", new { date, planDayId });

    public static Task<HttpResponseMessage> SaveSetsAsync(HttpClient client, int sessionId, params (int ExerciseId, int SetNumber, int Reps, double? WeightKg, bool Completed)[] sets) =>
        client.PutAsJsonAsync($"/api/workouts/sessions/{sessionId}/sets", new
        {
            sets = sets.Select(s => new { exerciseId = s.ExerciseId, setNumber = s.SetNumber, reps = s.Reps, weightKg = s.WeightKg, completed = s.Completed }).ToArray(),
        });
}
