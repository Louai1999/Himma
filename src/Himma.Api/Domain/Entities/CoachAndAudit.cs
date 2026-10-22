namespace Himma.Api.Domain.Entities;

public class CoachMessage
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public CoachRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Admin actions and admin views of user data. Deliberately has no foreign keys,
/// so entries survive when the actor or target account is purged.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string ActorRole { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? TargetUserId { get; set; }

    /// <summary>JSON with context for the action. Never contains passwords or tokens.</summary>
    public string Details { get; set; } = "{}";

    public string? IpAddress { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
