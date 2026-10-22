using System.Net.Http.Headers;
using System.Net.Http.Json;
using Himma.Api.Features.Auth;
using Himma.Api.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Himma.Tests.Auth;

/// <summary>Small helpers that call the auth endpoints the way the browser does.</summary>
internal static class AuthApi
{
    /// <summary>HTTPS client without automatic cookies, so tests control exactly which refresh cookie is sent.</summary>
    public static HttpClient CreateHttpsClient(this WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
            AllowAutoRedirect = false,
        });

    public static string NewUsername() => "u" + Guid.NewGuid().ToString("N")[..12];

    /// <summary>Random strong password per call, so no password is stored in the repository.</summary>
    public static string NewPassword() => "Hm-" + Guid.NewGuid().ToString("N")[..20];

    public static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string username, string password, bool acceptPrivacy = true) =>
        client.PostAsJsonAsync("/api/auth/register", new { username, password, displayName = "مستخدم تجريبي", acceptPrivacy });

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string username, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { username, password });

    public static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string? refreshCookie) =>
        client.SendAsync(WithCookie(new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh"), refreshCookie));

    public static Task<HttpResponseMessage> LogoutAsync(HttpClient client, string? refreshCookie) =>
        client.SendAsync(WithCookie(new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout"), refreshCookie));

    public static Task<HttpResponseMessage> ChangePasswordAsync(HttpClient client, string? accessToken, string currentPassword, string newPassword)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Content = JsonContent.Create(new { currentPassword, newPassword }),
        };
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        return client.SendAsync(request);
    }

    /// <summary>Registers a fresh user and returns everything a test needs about the session.</summary>
    public static async Task<Session> RegisterNewUserAsync(HttpClient client)
    {
        var username = NewUsername();
        var password = NewPassword();
        var response = await RegisterAsync(client, username, password);
        response.EnsureSuccessStatusCode();
        return await Session.FromAsync(response, username, password);
    }

    /// <summary>The full Set-Cookie header for the refresh cookie, or null.</summary>
    public static string? RefreshSetCookieHeader(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(RefreshCookie.Name + "=", StringComparison.Ordinal))
            : null;

    /// <summary>Just the cookie value, or null if the response did not set (or cleared) it.</summary>
    public static string? RefreshCookieValue(HttpResponseMessage response)
    {
        var header = RefreshSetCookieHeader(response);
        if (header is null)
        {
            return null;
        }
        var value = header[(RefreshCookie.Name.Length + 1)..].Split(';')[0];
        return value.Length == 0 ? null : value;
    }

    private static HttpRequestMessage WithCookie(HttpRequestMessage request, string? refreshCookie)
    {
        if (refreshCookie is not null)
        {
            request.Headers.Add("Cookie", $"{RefreshCookie.Name}={refreshCookie}");
        }
        return request;
    }

    public sealed record Session(string Username, string Password, string UserId, string AccessToken, string RefreshCookie)
    {
        public static async Task<Session> FromAsync(HttpResponseMessage response, string username, string password)
        {
            var body = await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options)
                ?? throw new InvalidOperationException("Empty auth response.");
            var cookie = RefreshCookieValue(response)
                ?? throw new InvalidOperationException("No refresh cookie in auth response.");
            return new Session(username, password, body.User.Id, body.AccessToken, cookie);
        }
    }
}
