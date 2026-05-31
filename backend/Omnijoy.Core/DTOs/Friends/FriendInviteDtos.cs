namespace Omnijoy.Core.DTOs.Friends;

/// <summary>Returned when a new invite link is created.</summary>
public record FriendInviteLinkDto(
    string Token,
    string InviteUrl,
    DateTime ExpiresAt
);

/// <summary>Returned when an email invite is sent or a user already exists.</summary>
public record FriendInviteEmailResultDto(
    /// <summary>
    /// "invited" — email sent, user didn't exist yet.
    /// "accepted" — user with that email already existed; friendship auto-accepted.
    /// </summary>
    string Outcome,
    string? DisplayName
);

/// <summary>Request body for email invite.</summary>
public record FriendInviteEmailRequest(string Email);

/// <summary>Result returned when an invite token is redeemed.</summary>
public record FriendInviteAcceptDto(
    string InviterDisplayName,
    string? InviterAvatarUrl,
    Guid InviterId
);
