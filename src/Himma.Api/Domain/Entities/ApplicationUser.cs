using Microsoft.AspNetCore.Identity;

namespace Himma.Api.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }

    /// <summary>False when suspended or soft-deleted. Checked on every authenticated request.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime? DeletedAtUtc { get; set; }
    public DateTime? DeletionScheduledForUtc { get; set; }
    public DateTime PrivacyConsentAtUtc { get; set; }
}
