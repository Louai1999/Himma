using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Himma.Api.Features.Account;
using Himma.Api.Features.Admin;
using Himma.Api.Features.Auth;
using Himma.Api.Infrastructure.Audit;
using Himma.Tests.Auth;
using Himma.Tests.Tracking;
using Microsoft.AspNetCore.Mvc;

namespace Himma.Tests.Admin;

/// <summary>The user's own "export my data" and "delete my account".</summary>
public sealed class AccountTests(HimmaApiFactory factory) : IClassFixture<HimmaApiFactory>
{
    private static Task<HttpResponseMessage> DeleteAccountAsync(HttpClient client, string password, string? refreshCookie = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/account") { Content = JsonContent.Create(new { password }) };
        if (refreshCookie is not null)
        {
            request.Headers.Add("Cookie", $"{Himma.Api.Infrastructure.Auth.RefreshCookie.Name}={refreshCookie}");
        }
        return client.SendAsync(request);
    }

    [Fact]
    public async Task Export_contains_only_the_users_own_data()
    {
        var user = await factory.SignUpAsync();
        var other = await factory.SignUpAsync();
        await UserApi.PutProfileAsync(user.Client);
        await UserApi.AddAiMealAsync(user.Client, UserApi.Today, 444);
        await UserApi.AddAiMealAsync(other.Client, UserApi.Today, 999);

        var response = await user.Client.GetAsync("/api/account/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(user.Session.Username, root.GetProperty("account").GetProperty("userName").GetString());
        var meal = Assert.Single(root.GetProperty("meals").EnumerateArray());
        Assert.Equal(444, meal.GetProperty("calories").GetDouble());
        Assert.Equal("male", root.GetProperty("profile").GetProperty("gender").GetString());
        Assert.DoesNotContain("passwordHash", root.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await AdminApi.AuditCountAsync(factory, AuditActions.SelfExport, user.Session.UserId));
    }

    [Fact]
    public async Task Deleting_the_account_needs_the_password_and_starts_the_deletion_flow()
    {
        var user = await factory.SignUpAsync();

        var wrong = await DeleteAccountAsync(user.Client, AuthApi.NewPassword());
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.True((await wrong.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.ContainsKey("password"));

        var deleted = await DeleteAccountAsync(user.Client, user.Session.Password, user.Session.RefreshCookie);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Null(AuthApi.RefreshCookieValue(deleted)); // cookie cleared

        // Signed out everywhere, and sign-in explains the account is being deleted.
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.Client.GetAsync("/api/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthApi.RefreshAsync(factory.CreateHttpsClient(), user.Session.RefreshCookie)).StatusCode);
        var login = await AuthApi.LoginAsync(factory.CreateHttpsClient(), user.Session.Username, user.Session.Password);
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Equal("الحساب مجدول للحذف", (await login.Content.ReadFromJsonAsync<ProblemDetails>())!.Title);

        // The admin sees it under "pending deletion" and can restore it within the grace period.
        var admin = await factory.SignInAsAdminAsync();
        var pending = await admin.GetFromJsonAsync<PagedResult<AdminUserRow>>(
            $"/api/admin/users?search={user.Session.Username}&status=pendingDeletion", TestJson.Options);
        Assert.Equal(AccountStatus.PendingDeletion, Assert.Single(pending!.Items).Status);
        Assert.Equal(1, await AdminApi.AuditCountAsync(factory, AuditActions.SelfDelete, user.Session.UserId));
    }

    [Fact]
    public async Task The_last_admin_cannot_delete_their_own_account()
    {
        var admin = await factory.SignInAsAdminAsync();

        var response = await DeleteAccountAsync(admin, factory.AdminPassword);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var stillWorks = await AuthApi.LoginAsync(factory.CreateHttpsClient(), HimmaApiFactory.AdminUsername, factory.AdminPassword);
        Assert.Equal(HttpStatusCode.OK, stillWorks.StatusCode);
    }

    [Fact]
    public async Task Account_endpoints_require_sign_in()
    {
        var anonymous = factory.CreateHttpsClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/account/export")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await DeleteAccountAsync(anonymous, "x")).StatusCode);
    }
}
