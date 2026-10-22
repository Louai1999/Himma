using System.Net;
using System.Net.Http.Json;
using Himma.Api.Data;
using Himma.Api.Domain;
using Himma.Api.Features.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Himma.Tests.Auth;

public sealed class AuthTests(HimmaApiFactory factory) : IClassFixture<HimmaApiFactory>
{
    private readonly HttpClient _client = factory.CreateHttpsClient();

    // ---------- Register ----------

    [Fact]
    public async Task Register_returns_access_token_and_a_locked_down_refresh_cookie()
    {
        var response = await AuthApi.RegisterAsync(_client, AuthApi.NewUsername(), AuthApi.NewPassword());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrEmpty(body!.AccessToken));
        Assert.InRange(body.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(14), DateTime.UtcNow.AddMinutes(16));

        var cookie = AuthApi.RefreshSetCookieHeader(response)!.ToLowerInvariant();
        Assert.Contains("httponly", cookie);
        Assert.Contains("secure", cookie);
        Assert.Contains("samesite=strict", cookie);
        Assert.Contains("path=/api/auth", cookie);
    }

    [Fact]
    public async Task Registered_user_only_gets_the_User_role()
    {
        var response = await AuthApi.RegisterAsync(_client, AuthApi.NewUsername(), AuthApi.NewPassword());

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal([Roles.User], body!.User.Roles);
    }

    [Fact]
    public async Task Register_rejects_a_role_field_instead_of_ignoring_it()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = AuthApi.NewUsername(),
            password = AuthApi.NewPassword(),
            displayName = "مخترق",
            acceptPrivacy = true,
            role = Roles.Admin,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_a_taken_username_returns_conflict()
    {
        var username = AuthApi.NewUsername();
        await AuthApi.RegisterAsync(_client, username, AuthApi.NewPassword());

        var second = await AuthApi.RegisterAsync(_client, username.ToUpperInvariant(), AuthApi.NewPassword());

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_cannot_claim_the_admin_username()
    {
        var response = await AuthApi.RegisterAsync(_client, HimmaApiFactory.AdminUsername, AuthApi.NewPassword());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("short1")]          // under 8 characters
    [InlineData("password123")]     // well-known password
    [InlineData("aaaaaaaaaa")]      // one repeated character
    [InlineData("12345678")]        // simple sequence
    public async Task Register_rejects_weak_passwords(string password)
    {
        var response = await AuthApi.RegisterAsync(_client, AuthApi.NewUsername(), password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.True(problem!.Errors.ContainsKey("password"));
    }

    [Fact]
    public async Task Register_rejects_a_password_containing_the_username()
    {
        var username = AuthApi.NewUsername();

        var response = await AuthApi.RegisterAsync(_client, username, username + "!2024");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("ab")]
    [InlineData("مستخدم")]
    public async Task Register_rejects_invalid_usernames(string username)
    {
        var response = await AuthApi.RegisterAsync(_client, username, AuthApi.NewPassword());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_requires_privacy_consent()
    {
        var response = await AuthApi.RegisterAsync(_client, AuthApi.NewUsername(), AuthApi.NewPassword(), acceptPrivacy: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Passwords_are_stored_as_Identity_PBKDF2_hashes()
    {
        var session = await AuthApi.RegisterNewUserAsync(_client);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hash = await db.Users.Where(u => u.Id == session.UserId).Select(u => u.PasswordHash).SingleAsync();

        Assert.NotNull(hash);
        Assert.DoesNotContain(session.Password, hash);
        Assert.StartsWith("AQAAAA", hash); // Identity v3 format: PBKDF2 with HMAC-SHA512
    }

    // ---------- Login and lockout ----------

    [Fact]
    public async Task Login_succeeds_with_correct_password()
    {
        var session = await AuthApi.RegisterNewUserAsync(_client);

        var response = await AuthApi.LoginAsync(_client, session.Username, session.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(AuthApi.RefreshCookieValue(response));
    }

    [Fact]
    public async Task Login_failure_does_not_reveal_whether_the_username_exists()
    {
        var session = await AuthApi.RegisterNewUserAsync(_client);

        var wrongPassword = await AuthApi.LoginAsync(_client, session.Username, AuthApi.NewPassword());
        var unknownUser = await AuthApi.LoginAsync(_client, AuthApi.NewUsername(), AuthApi.NewPassword());

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        var a = await wrongPassword.Content.ReadFromJsonAsync<ProblemDetails>();
        var b = await unknownUser.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(a!.Title, b!.Title);
        Assert.Equal(a.Detail, b.Detail);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account()
    {
        var session = await AuthApi.RegisterNewUserAsync(_client);

        for (var i = 0; i < 5; i++)
        {
            await AuthApi.LoginAsync(_client, session.Username, AuthApi.NewPassword());
        }
        var withCorrectPassword = await AuthApi.LoginAsync(_client, session.Username, session.Password);

        Assert.Equal(HttpStatusCode.Unauthorized, withCorrectPassword.StatusCode);
    }

    // ---------- Refresh rotation and reuse detection ----------

    [Fact]
    public async Task Refresh_issues_a_new_access_token_and_rotates_the_cookie()
    {
        var session = await AuthApi.RegisterNewUserAsync(_client);

        var response = await AuthApi.RefreshAsync(_client, session.RefreshCookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var newCookie = AuthApi.RefreshCookieValue(response);
        Assert.NotNull(newCookie);
        Assert.NotEqual(session.RefreshCookie, newCookie);
    }

    [Fact]
    public async Task Reusing_an_old_refresh_token_revokes_the_whole_family()
    {
        var session = await AuthApi.RegisterNewUserAsync(_client);
        var rotated = AuthApi.RefreshCookieValue(await AuthApi.RefreshAsync(_client, session.RefreshCookie));

        var reuse = await AuthApi.RefreshAsync(_client, session.RefreshCookie);
        var newestAfterReuse = await AuthApi.RefreshAsync(_client, rotated);

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, newestAfterReuse.StatusCode);
    }

    [Fact]
    public async Task Refresh_without_or_with_a_made_up_cookie_is_rejected()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthApi.RefreshAsync(_client, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthApi.RefreshAsync(_client, "not-a-real-token")).StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token_and_clears_the_cookie()
    {
        var session = await AuthApi.RegisterNewUserAsync(_client);

        var logout = await AuthApi.LogoutAsync(_client, session.RefreshCookie);
        var refreshAfterLogout = await AuthApi.RefreshAsync(_client, session.RefreshCookie);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.NotNull(AuthApi.RefreshSetCookieHeader(logout));
        Assert.Null(AuthApi.RefreshCookieValue(logout));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }

    // ---------- Protected endpoints, suspension, password change ----------

    [Fact]
    public async Task Protected_endpoint_requires_a_valid_access_token()
    {
        var noToken = await AuthApi.ChangePasswordAsync(_client, null, "x", AuthApi.NewPassword());
        var forgedToken = await AuthApi.ChangePasswordAsync(_client, "eyJhbGciOiJIUzI1NiJ9.e30.forged", "x", AuthApi.NewPassword());

        Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, forgedToken.StatusCode);
    }

    [Theory]
    [InlineData(true)]   // suspended
    [InlineData(false)]  // soft-deleted
    public async Task Disabled_accounts_are_rejected_on_every_path(bool suspend)
    {
        var session = await AuthApi.RegisterNewUserAsync(_client);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var users = db.Users.Where(u => u.Id == session.UserId);
            if (suspend)
            {
                await users.ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
            }
            else
            {
                await users.ExecuteUpdateAsync(s => s.SetProperty(u => u.DeletedAtUtc, DateTime.UtcNow));
            }
        }

        var withAccessToken = await AuthApi.ChangePasswordAsync(_client, session.AccessToken, session.Password, AuthApi.NewPassword());
        var refresh = await AuthApi.RefreshAsync(_client, session.RefreshCookie);
        var login = await AuthApi.LoginAsync(_client, session.Username, session.Password);

        Assert.Equal(HttpStatusCode.Unauthorized, withAccessToken.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
    }

    [Fact]
    public async Task Change_password_signs_out_old_sessions_and_keeps_the_current_one()
    {
        var session = await AuthApi.RegisterNewUserAsync(_client);
        var newPassword = AuthApi.NewPassword();

        var changed = await AuthApi.ChangePasswordAsync(_client, session.AccessToken, session.Password, newPassword);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var fresh = await AuthApi.Session.FromAsync(changed, session.Username, newPassword);

        // Old access token and old refresh cookie no longer work.
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await AuthApi.ChangePasswordAsync(_client, session.AccessToken, newPassword, AuthApi.NewPassword())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthApi.RefreshAsync(_client, session.RefreshCookie)).StatusCode);

        // The old password is gone, the new one works, and the new session is valid.
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthApi.LoginAsync(_client, session.Username, session.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.LoginAsync(_client, session.Username, newPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.RefreshAsync(_client, fresh.RefreshCookie)).StatusCode);
    }

    [Fact]
    public async Task Change_password_with_wrong_current_password_is_rejected()
    {
        var session = await AuthApi.RegisterNewUserAsync(_client);

        var response = await AuthApi.ChangePasswordAsync(_client, session.AccessToken, AuthApi.NewPassword(), AuthApi.NewPassword());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.True(problem!.Errors.ContainsKey("currentPassword"));
    }

    // ---------- Rate limiting ----------

    [Fact]
    public async Task Auth_endpoints_are_rate_limited()
    {
        using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:Auth:PermitLimit", "3"));
        var client = limited.CreateHttpsClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            statuses.Add((await AuthApi.LoginAsync(client, AuthApi.NewUsername(), AuthApi.NewPassword())).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
        Assert.All(statuses.Take(3), s => Assert.Equal(HttpStatusCode.Unauthorized, s));
    }
}
