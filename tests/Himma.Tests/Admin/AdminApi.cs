using System.Net.Http.Headers;
using System.Net.Http.Json;
using Himma.Api.Data;
using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Features.Auth;
using Himma.Tests.Auth;
using Himma.Tests.Tracking;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Himma.Tests.Admin;

internal static class AdminApi
{
    /// <summary>Signs in as the seeded admin.</summary>
    public static async Task<HttpClient> SignInAsAdminAsync(this HimmaApiFactory factory)
    {
        var client = factory.CreateHttpsClient();
        var response = await AuthApi.LoginAsync(client, HimmaApiFactory.AdminUsername, factory.AdminPassword);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    public static Task<HttpResponseMessage> SuspendAsync(HttpClient admin, string userId, string password) =>
        admin.PostAsJsonAsync($"/api/admin/users/{userId}/suspend", new { adminPassword = password });

    public static Task<HttpResponseMessage> SoftDeleteAsync(HttpClient admin, string userId, string password) =>
        admin.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/users/{userId}")
        {
            Content = JsonContent.Create(new { adminPassword = password }),
        });

    public static Task<HttpResponseMessage> PurgeAsync(HttpClient admin, string userId, string password, string confirmUsername) =>
        admin.PostAsJsonAsync($"/api/admin/users/{userId}/purge", new { adminPassword = password, confirmUsername });

    /// <summary>Makes an existing user an admin directly in the database (there is no API for that on purpose).</summary>
    public static async Task PromoteToAdminAsync(HimmaApiFactory factory, string userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await users.AddToRoleAsync((await users.FindByIdAsync(userId))!, Roles.Admin);
    }

    public static async Task<T> WithDbAsync<T>(HimmaApiFactory factory, Func<AppDbContext, Task<T>> query)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public static Task<int> AuditCountAsync(HimmaApiFactory factory, string action, string targetUserId) =>
        WithDbAsync(factory, db => db.AuditLogs.CountAsync(l => l.Action == action && l.TargetUserId == targetUserId));
}
