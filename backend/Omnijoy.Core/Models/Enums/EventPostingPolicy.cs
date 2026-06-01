namespace Omnijoy.Core.Models.Enums;

/// <summary>
/// Controls who may create posts on an event's wall.
/// </summary>
public enum EventPostingPolicy
{
    /// <summary>Only the event organiser (creator or company-page admin) may post.</summary>
    OrganizerOnly,

    /// <summary>Any user who can see the event may post.</summary>
    Everyone,
}
