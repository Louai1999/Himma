using System.Security.Cryptography;
using Himma.Api.Data;
using Himma.Api.Domain.Entities;
using Himma.Api.Options;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Himma.Api.Infrastructure.Push;

/// <summary>VAPID keys as base64url: the public key is the uncompressed P-256 point, the private key its scalar.</summary>
public sealed record VapidKeys(string PublicKey, string PrivateKey);

/// <summary>
/// Holds the push signing keys. They come from configuration when both are set; otherwise a pair is created
/// once and stored in the database, so every restart (and every server) uses the same keys and existing
/// subscriptions keep working.
/// </summary>
public sealed class PushKeyStore(IOptions<PushOptions> options)
{
    private const string PublicKeySetting = "push.vapid.publicKey";
    private const string PrivateKeySetting = "push.vapid.privateKey";

    private VapidKeys? keys;

    public VapidKeys Keys => keys ?? throw new InvalidOperationException("Push keys are not loaded yet.");

    public async Task EnsureAsync(AppDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        var configured = options.Value;
        if (!string.IsNullOrWhiteSpace(configured.PublicKey) && !string.IsNullOrWhiteSpace(configured.PrivateKey))
        {
            keys = new VapidKeys(configured.PublicKey.Trim(), configured.PrivateKey.Trim());
            return;
        }

        var stored = await db.AppSettings.AsNoTracking()
            .Where(s => s.Key == PublicKeySetting || s.Key == PrivateKeySetting)
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);
        if (stored.TryGetValue(PublicKeySetting, out var publicKey) && stored.TryGetValue(PrivateKeySetting, out var privateKey))
        {
            keys = new VapidKeys(publicKey, privateKey);
            return;
        }

        keys = Generate();
        db.AppSettings.RemoveRange(db.AppSettings.Where(s => s.Key == PublicKeySetting || s.Key == PrivateKeySetting));
        db.AppSettings.Add(new AppSetting { Key = PublicKeySetting, Value = keys.PublicKey });
        db.AppSettings.Add(new AppSetting { Key = PrivateKeySetting, Value = keys.PrivateKey });
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created and stored a new push key pair");
    }

    public static VapidKeys Generate()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = ecdsa.ExportParameters(includePrivateParameters: true);
        byte[] uncompressedPoint = [0x04, .. parameters.Q.X!, .. parameters.Q.Y!];
        return new VapidKeys(WebEncoders.Base64UrlEncode(uncompressedPoint), WebEncoders.Base64UrlEncode(parameters.D!));
    }
}
