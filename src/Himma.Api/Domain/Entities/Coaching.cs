namespace Himma.Api.Domain.Entities;

/// <summary>
/// A paid coach package, switched on by the admin for one user. The user is a coach while
/// <see cref="ActiveUntilUtc"/> is in the future; this is checked from the database on every
/// coach request, so ending or extending a package takes effect at once.
/// </summary>
public class CoachAccount
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    /// <summary>The name clients see, e.g. the coach's own name or the gym's.</summary>
    public string BusinessName { get; set; } = string.Empty;

    public string? Bio { get; set; }

    /// <summary>Short code clients type (or open as a link) to join. The coach can change it at any time.</summary>
    public string InviteCode { get; set; } = string.Empty;

    /// <summary>How many active clients the package allows (the seats the coach paid for).</summary>
    public int MaxClients { get; set; }

    public DateTime ActiveUntilUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>
/// A client who chose to share their data with a coach. Only the client can start it (by entering the
/// coach's code); either side can end it, and ending it removes the coach's access immediately.
/// Ended links are kept so both sides can see the history of who coached whom.
/// </summary>
public class CoachClient
{
    public int Id { get; set; }
    public string CoachUserId { get; set; } = string.Empty;
    public string ClientUserId { get; set; } = string.Empty;
    public CoachLinkStatus Status { get; set; }
    public DateTime JoinedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }

    /// <summary>The coach's own notes about the client. Never shown to the client.</summary>
    public string? PrivateNote { get; set; }
}

/// <summary>A short message from a coach to one client, shown in the client's app (and sent as a push notification).</summary>
public class CoachNote
{
    public int Id { get; set; }
    public string CoachUserId { get; set; } = string.Empty;
    public string ClientUserId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}
