using System.Net;
using System.Net.Http.Json;
using Himma.Api.Features.Tips;
using Himma.Tests.Auth;

namespace Himma.Tests.Tracking;

public sealed class TipsTests(HimmaApiFactory factory) : IClassFixture<HimmaApiFactory>
{
    [Fact]
    public void There_are_at_least_30_tips()
    {
        Assert.True(DailyTips.All.Count >= 30);
        Assert.Equal(DailyTips.All.Count, DailyTips.All.Distinct().Count());
    }

    [Fact]
    public async Task Tip_depends_on_the_date_and_is_stable()
    {
        var user = await factory.SignUpAsync();

        var first = await user.Client.GetFromJsonAsync<TipDto>("/api/tips/today?date=2026-01-01", TestJson.Options);
        var again = await user.Client.GetFromJsonAsync<TipDto>("/api/tips/today?date=2026-01-01", TestJson.Options);
        var next = await user.Client.GetFromJsonAsync<TipDto>("/api/tips/today?date=2026-01-02", TestJson.Options);

        Assert.Equal(first!.Text, again!.Text);
        Assert.NotEqual(first.Text, next!.Text);
        Assert.Equal(DailyTips.All[0], first.Text);
    }

    [Fact]
    public async Task Tip_requires_sign_in_and_a_valid_date()
    {
        var user = await factory.SignUpAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await user.Client.GetAsync("/api/tips/today?date=yesterday")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateHttpsClient().GetAsync("/api/tips/today")).StatusCode);
    }
}
