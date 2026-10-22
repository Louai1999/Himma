namespace Himma.Api.Domain.Entities;

public class RefreshToken
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    /// <summary>SHA-256 of the raw token. The raw value only ever exists in the user's cookie.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>All tokens created by rotating one login share a family; reuse of an old one revokes the family.</summary>
    public Guid FamilyId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public int? ReplacedByTokenId { get; set; }
    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }
}
