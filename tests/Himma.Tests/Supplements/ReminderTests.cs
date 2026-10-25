using System.Net;
using System.Net.Http.Json;
using Himma.Api.Features.Push;
using Himma.Api.Infrastructure.Push;
using Himma.Tests.Admin;
using Himma.Tests.Auth;
using Himma.Tests.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Himma.Tests.Supplements;

public sealed class ReminderTests(HimmaApiFactory factory) : IClassFixture<HimmaApiFactory>
{
    private static readonly DateOnly Today = DateOnly.Parse(UserApi.Today);

    private static string NewEndpoint() => $"https://fcm.googleapis.com/fcm/send/test-{Guid.NewGuid():N}";

    private static Task<HttpResponseMessage> SubscribeAsync(HttpClient client, string endpoint, string timeZone = "UTC", string language = "ar") =>
        client.PostAsJsonAsync("/api/push/subscriptions", new
        {
            endpoint,
            p256dh = "BMlzmS0qrLJ6pW8w3x3Wcx3PxR6M1vZ1bT0g9l8u4gX0",
            auth = "k3Jd7yR2hQ0wXz9a",
            timeZone,
            language,
        });

    private Task<int> RunAsync(DateTime fromUtc, DateTime toUtc) =>
        factory.Services.GetRequiredService<ReminderService>().RunAsync(fromUtc, toUtc, CancellationToken.None);

    private static DateTime UtcAt(int hour, int minute) => Today.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Utc);

    // ---------- Devices ----------

    [Fact]
    public async Task The_public_key_is_available_to_signed_in_users()
    {
        var user = await factory.SignUpAsync();

        var key = await user.Client.GetFromJsonAsync<PushKeyResponse>("/api/push/key", TestJson.Options);

        Assert.Equal(87, key!.PublicKey.Length);   // base64url of the 65-byte uncompressed P-256 point
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateHttpsClient().GetAsync("/api/push/key")).StatusCode);
    }

    [Fact]
    public async Task A_device_can_subscribe_and_unsubscribe()
    {
        var user = await factory.SignUpAsync();
        var endpoint = NewEndpoint();

        Assert.Equal(HttpStatusCode.NoContent, (await SubscribeAsync(user.Client, endpoint, "Asia/Muscat")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SubscribeAsync(user.Client, endpoint, "Asia/Muscat")).StatusCode);
        Assert.Equal(1, (await user.Client.GetFromJsonAsync<PushStatusResponse>("/api/push/devices", TestJson.Options))!.Devices);

        await user.Client.PostAsJsonAsync("/api/push/subscriptions/delete", new { endpoint });
        Assert.Equal(0, (await user.Client.GetFromJsonAsync<PushStatusResponse>("/api/push/devices", TestJson.Options))!.Devices);
    }

    [Theory]
    [InlineData("http://fcm.googleapis.com/fcm/send/x")]            // not https
    [InlineData("https://localhost/push")]                           // internal address
    [InlineData("https://169.254.169.254/latest/meta-data")]         // cloud metadata
    [InlineData("https://evilgoogleapis.com/fcm/send/x")]            // look-alike host
    [InlineData("https://fcm.googleapis.com.evil.com/x")]
    [InlineData("https://fcm.googleapis.com:8443/fcm/send/x")]       // unusual port
    [InlineData("https://user@fcm.googleapis.com/fcm/send/x")]
    public async Task Only_real_push_services_are_accepted(string endpoint)
    {
        var user = await factory.SignUpAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await SubscribeAsync(user.Client, endpoint)).StatusCode);
    }

    [Theory]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/x")]
    [InlineData("https://web.push.apple.com/QK2x")]
    [InlineData("https://wns2-par02p.notify.windows.com/w/?token=x")]
    public async Task Firefox_safari_and_edge_push_services_are_accepted(string endpoint)
    {
        var user = await factory.SignUpAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await SubscribeAsync(user.Client, endpoint + Guid.NewGuid().ToString("N"))).StatusCode);
    }

    [Fact]
    public async Task Unknown_time_zones_are_rejected()
    {
        var user = await factory.SignUpAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await SubscribeAsync(user.Client, NewEndpoint(), "Mars/Olympus")).StatusCode);
    }

    [Fact]
    public async Task A_browser_moves_to_whoever_subscribes_with_it_last()
    {
        var a = await factory.SignUpAsync();
        var b = await factory.SignUpAsync();
        var endpoint = NewEndpoint();

        await SubscribeAsync(a.Client, endpoint);
        await SubscribeAsync(b.Client, endpoint);

        Assert.Equal(0, (await a.Client.GetFromJsonAsync<PushStatusResponse>("/api/push/devices", TestJson.Options))!.Devices);
        Assert.Equal(1, (await b.Client.GetFromJsonAsync<PushStatusResponse>("/api/push/devices", TestJson.Options))!.Devices);
    }

    [Fact]
    public async Task Users_cannot_unsubscribe_someone_elses_device()
    {
        var a = await factory.SignUpAsync();
        var b = await factory.SignUpAsync();
        var endpoint = NewEndpoint();
        await SubscribeAsync(a.Client, endpoint);

        await b.Client.PostAsJsonAsync("/api/push/subscriptions/delete", new { endpoint });

        Assert.Equal(1, (await a.Client.GetFromJsonAsync<PushStatusResponse>("/api/push/devices", TestJson.Options))!.Devices);
    }

    [Fact]
    public async Task The_test_notification_goes_to_the_users_devices_only()
    {
        var a = await factory.SignUpAsync();
        var b = await factory.SignUpAsync();
        var aEndpoint = NewEndpoint();
        var bEndpoint = NewEndpoint();
        await SubscribeAsync(a.Client, aEndpoint);
        await SubscribeAsync(b.Client, bEndpoint);

        var result = await (await a.Client.PostAsync("/api/push/test", null)).Content.ReadFromJsonAsync<PushTestResponse>(TestJson.Options);

        Assert.Equal(1, result!.Sent);
        Assert.Single(factory.Push.To(aEndpoint));
        Assert.Empty(factory.Push.To(bEndpoint));
    }

    // ---------- Reminders ----------

    [Fact]
    public async Task A_supplement_is_reminded_at_its_local_time_with_a_taken_button()
    {
        var user = await factory.SignUpAsync();
        var endpoint = NewEndpoint();
        await SubscribeAsync(user.Client, endpoint, "Asia/Dubai");   // UTC+4, no daylight saving
        await SupplementApi.CreateDtoAsync(user.Client, "كرياتين", times: ["08:00"]);

        await RunAsync(UtcAt(3, 59), UtcAt(4, 0));   // 08:00 in Dubai

        var reminder = Assert.Single(factory.Push.To(endpoint));
        Assert.Contains("كرياتين", reminder.Title);
        Assert.Contains("5", reminder.Body);
        Assert.NotNull(reminder.TakeToken);
        Assert.Equal("أخذتها ✅", reminder.TakeLabel);
    }

    [Fact]
    public async Task Reminders_use_the_language_of_the_device()
    {
        var user = await factory.SignUpAsync();
        var endpoint = NewEndpoint();
        await SubscribeAsync(user.Client, endpoint, "UTC", "en");
        await SupplementApi.CreateDtoAsync(user.Client, "Creatine", times: ["10:00"]);

        await RunAsync(UtcAt(9, 59), UtcAt(10, 0));

        var reminder = Assert.Single(factory.Push.To(endpoint));
        Assert.Equal("Time for Creatine 💊", reminder.Title);
        Assert.Equal("Taken ✅", reminder.TakeLabel);
    }

    [Fact]
    public async Task A_dose_already_logged_is_not_reminded()
    {
        var user = await factory.SignUpAsync();
        var endpoint = NewEndpoint();
        await SubscribeAsync(user.Client, endpoint);
        var creatine = await SupplementApi.CreateDtoAsync(user.Client, times: ["11:00"]);
        await SupplementApi.TakeAsync(user.Client, creatine.Id);

        await RunAsync(UtcAt(10, 59), UtcAt(11, 0));
        await RunAsync(UtcAt(11, 59), UtcAt(12, 0));   // the "missed" check an hour later

        Assert.Empty(factory.Push.To(endpoint));
    }

    [Fact]
    public async Task A_dose_still_not_logged_an_hour_later_gets_a_second_reminder()
    {
        var user = await factory.SignUpAsync();
        var endpoint = NewEndpoint();
        await SubscribeAsync(user.Client, endpoint);
        await SupplementApi.CreateDtoAsync(user.Client, "زنك", times: ["13:00"]);

        await RunAsync(UtcAt(13, 59), UtcAt(14, 0));

        var reminder = Assert.Single(factory.Push.To(endpoint));
        Assert.Contains("ما سجّلت", reminder.Title);
        Assert.NotNull(reminder.TakeToken);
    }

    [Fact]
    public async Task Nothing_is_sent_outside_the_window_or_on_days_off()
    {
        var user = await factory.SignUpAsync();
        var endpoint = NewEndpoint();
        await SubscribeAsync(user.Client, endpoint);
        var dayOff = Himma.Api.Domain.Supplements.SupplementSchedule.DayKeyOf(Today).ToString().ToLowerInvariant();
        var otherDays = new[] { "sat", "sun", "mon", "tue", "wed", "thu", "fri" }.Where(d => d != dayOff).ToArray();
        await SupplementApi.CreateAsync(user.Client, "حديد", times: ["15:00"], days: otherDays);
        await SupplementApi.CreateDtoAsync(user.Client, "مغنيسيوم", times: ["15:30"]);

        await RunAsync(UtcAt(14, 59), UtcAt(15, 0));   // iron is off today
        await RunAsync(UtcAt(15, 28), UtcAt(15, 29));  // magnesium is not due yet

        Assert.Empty(factory.Push.To(endpoint));
    }

    [Fact]
    public async Task Water_reminders_follow_the_interval_the_user_chose()
    {
        var user = await factory.SignUpAsync();
        var endpoint = NewEndpoint();
        await SubscribeAsync(user.Client, endpoint);
        await user.Client.PutAsJsonAsync("/api/water/reminder", new { enabled = true, intervalHours = 3 });

        await RunAsync(UtcAt(8, 59), UtcAt(9, 0));    // 09:00: first reminder
        await RunAsync(UtcAt(9, 59), UtcAt(10, 0));   // 10:00: not on the 3-hour step
        await RunAsync(UtcAt(11, 59), UtcAt(12, 0));  // 12:00: second reminder

        Assert.Equal(2, factory.Push.To(endpoint).Count(p => p.Tag == "water"));
    }

    [Fact]
    public async Task Devices_the_push_service_reports_gone_are_forgotten()
    {
        var user = await factory.SignUpAsync();
        var endpoint = NewEndpoint();
        await SubscribeAsync(user.Client, endpoint);
        await SupplementApi.CreateDtoAsync(user.Client, times: ["16:00"]);
        factory.Push.GoneEndpoints[endpoint] = true;

        await RunAsync(UtcAt(15, 59), UtcAt(16, 0));

        Assert.Equal(0, await AdminApi.WithDbAsync(factory, db => db.PushDevices.CountAsync(d => d.Endpoint == endpoint)));
    }

    [Fact]
    public async Task Suspended_accounts_get_no_reminders()
    {
        var user = await factory.SignUpAsync();
        var endpoint = NewEndpoint();
        await SubscribeAsync(user.Client, endpoint);
        await SupplementApi.CreateDtoAsync(user.Client, times: ["17:00"]);
        await AdminApi.WithDbAsync(factory, async db =>
        {
            var account = await db.Users.SingleAsync(u => u.Id == user.Session.UserId);
            account.IsActive = false;
            return await db.SaveChangesAsync();
        });

        await RunAsync(UtcAt(16, 59), UtcAt(17, 0));

        Assert.Empty(factory.Push.To(endpoint));
    }
}
