using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Himma.Api.Features.Account;
using Himma.Api.Features.Admin;
using Himma.Api.Features.Auth;
using Himma.Api.Infrastructure.Audit;
using Himma.Tests.Admin;
using Himma.Tests.Tracking;

namespace Himma.Tests.Auth;

public sealed class RecoveryTests(HimmaApiFactory factory) : IClassFixture<HimmaApiFactory>
{
    private static string NewEmail() => $"u{Guid.NewGuid():N}@example.com";

    /// <summary>Signs in again: confirming an email or resetting a password rotates the security stamp.</summary>
    private async Task<AuthResponse> SignInAsync(HttpClient client, string username, string password)
    {
        var response = await AuthApi.LoginAsync(client, username, password);
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return auth;
    }

    private async Task<string> AddConfirmedEmailAsync(SignedInUser user)
    {
        var address = NewEmail();
        (await user.Client.PostAsJsonAsync("/api/account/email", new { email = address, password = user.Session.Password })).EnsureSuccessStatusCode();
        var code = factory.Email.LastCodeTo(address);
        (await user.Client.PostAsJsonAsync("/api/account/email/confirm", new { email = address, code })).EnsureSuccessStatusCode();
        return address;
    }

    // ---------- Recovery email ----------

    [Fact]
    public async Task An_email_is_only_saved_after_its_code_is_confirmed()
    {
        var user = await factory.SignUpAsync();
        var address = NewEmail();

        var start = await user.Client.PostAsJsonAsync("/api/account/email", new { email = address, password = user.Session.Password });
        var before = await user.Client.GetFromJsonAsync<AccountEmailDto>("/api/account/email", TestJson.Options);
        var wrong = await user.Client.PostAsJsonAsync("/api/account/email/confirm", new { email = address, code = "000000" });
        var right = await user.Client.PostAsJsonAsync("/api/account/email/confirm", new { email = address, code = factory.Email.LastCodeTo(address) });

        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        Assert.Null(before!.Email);
        Assert.True(before.EmailEnabled);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);

        var auth = await SignInAsync(user.Client, user.Session.Username, user.Session.Password);
        Assert.True(auth.User.HasEmail);
        Assert.Equal(address, (await user.Client.GetFromJsonAsync<AccountEmailDto>("/api/account/email", TestJson.Options))!.Email);
    }

    [Fact]
    public async Task Adding_an_email_needs_the_password()
    {
        var user = await factory.SignUpAsync();
        var address = NewEmail();

        var response = await user.Client.PostAsJsonAsync("/api/account/email", new { email = address, password = "wrong-password-1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Email.To(address));
    }

    [Fact]
    public async Task An_email_confirmed_by_another_account_cannot_be_used()
    {
        var a = await factory.SignUpAsync();
        var b = await factory.SignUpAsync();
        var address = await AddConfirmedEmailAsync(a);

        var response = await b.Client.PostAsJsonAsync("/api/account/email", new { email = address.ToUpperInvariant(), password = b.Session.Password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Removing_the_email_needs_the_password()
    {
        var user = await factory.SignUpAsync();
        await AddConfirmedEmailAsync(user);
        await SignInAsync(user.Client, user.Session.Username, user.Session.Password);

        var denied = await user.Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/account/email") { Content = JsonContent.Create(new { password = "wrong-password-1" }) });
        var removed = await user.Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/account/email") { Content = JsonContent.Create(new { password = user.Session.Password }) });

        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        var auth = await SignInAsync(user.Client, user.Session.Username, user.Session.Password);
        Assert.False(auth.User.HasEmail);
    }

    // ---------- Forgot password ----------

    [Fact]
    public async Task Forgot_password_emails_the_username_and_a_code_that_resets_the_password_once()
    {
        var user = await factory.SignUpAsync();
        var address = await AddConfirmedEmailAsync(user);
        var anonymous = factory.CreateHttpsClient();

        var forgot = await anonymous.PostAsJsonAsync("/api/auth/forgot-password", new { email = address });
        var mail = factory.Email.To(address)[^1];
        var code = factory.Email.LastCodeTo(address);
        var newPassword = AuthApi.NewPassword();
        var reset = await anonymous.PostAsJsonAsync("/api/auth/reset-password", new { email = address, code, newPassword });
        var again = await anonymous.PostAsJsonAsync("/api/auth/reset-password", new { email = address, code, newPassword = AuthApi.NewPassword() });

        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        Assert.Contains(user.Session.Username, mail.Text);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);   // the code worked once only
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthApi.LoginAsync(anonymous, user.Session.Username, user.Session.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.LoginAsync(anonymous, user.Session.Username, newPassword)).StatusCode);
        // Every old session ends.
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthApi.RefreshAsync(anonymous, user.Session.RefreshCookie)).StatusCode);
    }

    [Fact]
    public async Task Forgot_password_answers_the_same_for_unknown_and_unconfirmed_emails_and_sends_nothing()
    {
        var user = await factory.SignUpAsync();
        var unconfirmed = NewEmail();
        await user.Client.PostAsJsonAsync("/api/account/email", new { email = unconfirmed, password = user.Session.Password });
        var sentBefore = factory.Email.To(unconfirmed).Count;
        var unknown = NewEmail();
        var anonymous = factory.CreateHttpsClient();

        var a = await anonymous.PostAsJsonAsync("/api/auth/forgot-password", new { email = unknown });
        var b = await anonymous.PostAsJsonAsync("/api/auth/forgot-password", new { email = unconfirmed });

        Assert.Equal(HttpStatusCode.Accepted, a.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, b.StatusCode);
        Assert.Empty(factory.Email.To(unknown));
        Assert.Equal(sentBefore, factory.Email.To(unconfirmed).Count);
    }

    [Fact]
    public async Task Wrong_reset_codes_lock_the_account_for_a_while()
    {
        var user = await factory.SignUpAsync();
        var address = await AddConfirmedEmailAsync(user);
        var anonymous = factory.CreateHttpsClient();
        await anonymous.PostAsJsonAsync("/api/auth/forgot-password", new { email = address });
        var code = factory.Email.LastCodeTo(address);
        var wrong = code == "111111" ? "222222" : "111111";

        for (var i = 0; i < 5; i++)
        {
            await anonymous.PostAsJsonAsync("/api/auth/reset-password", new { email = address, code = wrong, newPassword = AuthApi.NewPassword() });
        }
        var withRightCode = await anonymous.PostAsJsonAsync("/api/auth/reset-password", new { email = address, code, newPassword = AuthApi.NewPassword() });

        Assert.Equal(HttpStatusCode.BadRequest, withRightCode.StatusCode);
    }

    [Fact]
    public async Task Without_an_email_provider_recovery_says_to_contact_the_admin()
    {
        factory.Email.IsEnabled = false;
        try
        {
            var anonymous = factory.CreateHttpsClient();

            var options = await anonymous.GetFromJsonAsync<AuthOptionsResponse>("/api/auth/options", TestJson.Options);
            var forgot = await anonymous.PostAsJsonAsync("/api/auth/forgot-password", new { email = NewEmail() });

            Assert.False(options!.EmailEnabled);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, forgot.StatusCode);
        }
        finally
        {
            factory.Email.IsEnabled = true;
        }
    }

    // ---------- Admin reset ----------

    [Fact]
    public async Task Admin_reset_gives_a_temporary_password_that_must_be_changed()
    {
        var admin = await factory.SignInAsAdminAsync();
        var user = await factory.SignUpAsync();

        var response = await admin.PostAsJsonAsync($"/api/admin/users/{user.Session.UserId}/reset-password", new { adminPassword = factory.AdminPassword });
        var temporary = (await response.Content.ReadFromJsonAsync<TemporaryPasswordResponse>(TestJson.Options))!.TemporaryPassword;

        Assert.Equal(12, temporary.Length);
        var client = factory.CreateHttpsClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthApi.LoginAsync(client, user.Session.Username, user.Session.Password)).StatusCode);
        var auth = await SignInAsync(client, user.Session.Username, temporary);
        Assert.True(auth.User.MustChangePassword);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthApi.RefreshAsync(client, user.Session.RefreshCookie)).StatusCode);

        var newPassword = AuthApi.NewPassword();
        var changed = await AuthApi.ChangePasswordAsync(client, auth.AccessToken, temporary, newPassword);
        Assert.False((await changed.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!.User.MustChangePassword);
        Assert.Equal(1, await AdminApi.AuditCountAsync(factory, AuditActions.ResetPassword, user.Session.UserId));
    }

    [Fact]
    public async Task Admin_reset_needs_the_admins_password_and_an_admin()
    {
        var admin = await factory.SignInAsAdminAsync();
        var user = await factory.SignUpAsync();
        var other = await factory.SignUpAsync();

        var wrongPassword = await admin.PostAsJsonAsync($"/api/admin/users/{user.Session.UserId}/reset-password", new { adminPassword = "not-the-password" });
        var notAdmin = await other.Client.PostAsJsonAsync($"/api/admin/users/{user.Session.UserId}/reset-password", new { adminPassword = other.Session.Password });

        Assert.Equal(HttpStatusCode.BadRequest, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, notAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.LoginAsync(factory.CreateHttpsClient(), user.Session.Username, user.Session.Password)).StatusCode);
    }

    [Fact]
    public async Task Admins_cannot_reset_their_own_password_this_way()
    {
        var admin = await factory.SignInAsAdminAsync();
        var me = (await (await AuthApi.LoginAsync(factory.CreateHttpsClient(), HimmaApiFactory.AdminUsername, factory.AdminPassword))
            .Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!.User.Id;

        var response = await admin.PostAsJsonAsync($"/api/admin/users/{me}/reset-password", new { adminPassword = factory.AdminPassword });

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
