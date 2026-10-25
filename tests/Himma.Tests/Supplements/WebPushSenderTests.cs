using System.Net;
using System.Security.Cryptography;
using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Push;
using Himma.Api.Options;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Himma.Tests.Supplements;

/// <summary>The real sender (encryption and VAPID signing) against a fake push service.</summary>
public sealed class WebPushSenderTests
{
    private sealed class FakePushService(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public byte[] Body { get; private set; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            return new HttpResponseMessage(status);
        }
    }

    /// <summary>A subscription with real keys, like the ones a browser creates.</summary>
    private static PushDevice BrowserDevice()
    {
        using var browserKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var q = browserKey.ExportParameters(false).Q;
        return new PushDevice
        {
            Id = 1,
            Endpoint = "https://fcm.googleapis.com/fcm/send/abc123",
            P256dh = WebEncoders.Base64UrlEncode([0x04, .. q.X!, .. q.Y!]),
            Auth = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(16)),
        };
    }

    private static async Task<(WebPushSender Sender, FakePushService Service)> CreateAsync(HttpStatusCode status)
    {
        var service = new FakePushService(status);
        var generated = PushKeyStore.Generate();
        var options = Microsoft.Extensions.Options.Options.Create(new PushOptions { PublicKey = generated.PublicKey, PrivateKey = generated.PrivateKey });
        var store = new PushKeyStore(options);
        // Configured keys are used as they are, so no database is needed.
        await store.EnsureAsync(db: null!, NullLogger.Instance, CancellationToken.None);
        return (new WebPushSender(new HttpClient(service), store, options, NullLogger<WebPushSender>.Instance), service);
    }

    [Fact]
    public async Task Messages_are_encrypted_and_signed_with_the_generated_keys()
    {
        var (sender, service) = await CreateAsync(HttpStatusCode.Created);

        var result = await sender.SendAsync(BrowserDevice(), new PushPayload("كرياتين", "5 غ", "tag", "/#today"), CancellationToken.None);

        Assert.Equal(PushSendResult.Sent, result);
        Assert.Equal("https://fcm.googleapis.com/fcm/send/abc123", service.Request!.RequestUri!.ToString());
        Assert.StartsWith("vapid", service.Request.Headers.Authorization!.Scheme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aes128gcm", service.Request.Content!.Headers.ContentEncoding);
        Assert.NotEmpty(service.Body);
        Assert.True(service.Body.AsSpan().IndexOf("كرياتين"u8) < 0);   // the payload is not readable in transit
    }

    [Theory]
    [InlineData(HttpStatusCode.Gone, PushSendResult.Gone)]
    [InlineData(HttpStatusCode.NotFound, PushSendResult.Gone)]
    [InlineData(HttpStatusCode.TooManyRequests, PushSendResult.Failed)]
    public async Task Push_service_answers_are_mapped(HttpStatusCode status, PushSendResult expected)
    {
        var (sender, _) = await CreateAsync(status);

        var result = await sender.SendAsync(BrowserDevice(), new PushPayload("t", "b", "tag", "/"), CancellationToken.None);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Generated_keys_have_the_sizes_browsers_expect()
    {
        var keys = PushKeyStore.Generate();

        Assert.Equal(65, WebEncoders.Base64UrlDecode(keys.PublicKey).Length);
        Assert.Equal(32, WebEncoders.Base64UrlDecode(keys.PrivateKey).Length);
    }
}
