using System.Security.Cryptography;
using System.Text;
using Himma.Api.Data;
using Himma.Api.Domain.Entities;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Himma.Api.Infrastructure.Auth;

public sealed record ClientInfo(string? IpAddress, string? UserAgent);

public sealed record RotationResult(bool Succeeded, string? UserId = null, string? NewRawToken = null)
{
    public static readonly RotationResult Failed = new(false);
}

/// <summary>
/// Refresh tokens: random values whose SHA-256 hash is stored. Every use rotates the token.
/// Using a token that was already rotated or revoked means it was probably stolen, so the
/// whole family (every token descended from the same login) is revoked.
/// </summary>
public sealed class RefreshTokenService(AppDbContext db, IOptions<JwtOptions> options, ILogger<RefreshTokenService> logger)
{
    public async Task<string> IssueAsync(string userId, Guid familyId, ClientInfo client, CancellationToken cancellationToken)
    {
        var (_, raw) = await CreateAsync(userId, familyId, client, cancellationToken);
        return raw;
    }

    public async Task<RotationResult> RotateAsync(string rawToken, ClientInfo client, CancellationToken cancellationToken)
    {
        var hash = Hash(rawToken);
        var token = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null)
        {
            return RotationResult.Failed;
        }

        if (token.RevokedAtUtc is not null)
        {
            await RevokeReusedFamilyAsync(token, cancellationToken);
            return RotationResult.Failed;
        }

        var now = DateTime.UtcNow;
        if (token.ExpiresAtUtc <= now)
        {
            return RotationResult.Failed;
        }

        // Revoke only if still active. If two requests race with the same token, only one wins;
        // the loser is treated as reuse.
        var claimed = await db.RefreshTokens
            .Where(t => t.Id == token.Id && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), cancellationToken);
        if (claimed == 0)
        {
            await RevokeReusedFamilyAsync(token, cancellationToken);
            return RotationResult.Failed;
        }

        var (replacement, raw) = await CreateAsync(token.UserId, token.FamilyId, client, cancellationToken);
        await db.RefreshTokens
            .Where(t => t.Id == token.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ReplacedByTokenId, replacement.Id), cancellationToken);

        return new RotationResult(true, token.UserId, raw);
    }

    /// <summary>Logout: revokes the family the given token belongs to. Unknown tokens are ignored.</summary>
    public async Task RevokeFamilyOfAsync(string rawToken, CancellationToken cancellationToken)
    {
        var hash = Hash(rawToken);
        var familyId = await db.RefreshTokens
            .Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.FamilyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (familyId is not null)
        {
            await RevokeFamilyAsync(familyId.Value, cancellationToken);
        }
    }

    /// <summary>Signs the user out everywhere (password change, suspension, deletion).</summary>
    public Task RevokeAllForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), cancellationToken);
    }

    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private async Task RevokeReusedFamilyAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        logger.LogWarning("Refresh token reuse detected for user {UserId}; revoking token family {FamilyId}", token.UserId, token.FamilyId);
        await RevokeFamilyAsync(token.FamilyId, cancellationToken);
    }

    private Task RevokeFamilyAsync(Guid familyId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), cancellationToken);
    }

    private async Task<(RefreshToken Entity, string Raw)> CreateAsync(string userId, Guid familyId, ClientInfo client, CancellationToken cancellationToken)
    {
        var raw = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var now = DateTime.UtcNow;
        var entity = new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(raw),
            FamilyId = familyId,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(options.Value.RefreshTokenDays),
            CreatedByIp = Truncate(client.IpAddress, 64),
            UserAgent = Truncate(client.UserAgent, 256),
        };

        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return (entity, raw);
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
