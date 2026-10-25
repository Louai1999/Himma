using System.Globalization;
using System.Security.Cryptography;
using Himma.Api.Infrastructure.Validation;
using Microsoft.AspNetCore.DataProtection;

namespace Himma.Api.Features.Supplements;

public sealed record TakeTokenData(string UserId, int SupplementId, DateOnly Date);

/// <summary>
/// The "Taken" button on a notification runs in the service worker, which has no access token.
/// Each reminder therefore carries a signed, encrypted token that can only log that one supplement for
/// that one day, and expires after <see cref="Lifetime"/>. It cannot read or change anything else.
/// </summary>
public sealed class TakeTokens(IDataProtectionProvider provider)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(36);

    private readonly ITimeLimitedDataProtector protector =
        provider.CreateProtector("Himma.SupplementTake.v1").ToTimeLimitedDataProtector();

    public string Create(string userId, int supplementId, DateOnly date) =>
        protector.Protect(
            string.Join('|', userId, supplementId.ToString(CultureInfo.InvariantCulture), date.ToString(ApiDate.Format, CultureInfo.InvariantCulture)),
            Lifetime);

    /// <summary>Null when the token is forged, damaged or expired.</summary>
    public TakeTokenData? Read(string token)
    {
        try
        {
            var parts = protector.Unprotect(token).Split('|');
            return parts.Length == 3
                && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var supplementId)
                && DateOnly.TryParseExact(parts[2], ApiDate.Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? new TakeTokenData(parts[0], supplementId, date)
                : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
