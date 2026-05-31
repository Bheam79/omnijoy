using Omnijoy.Core.DTOs.Friends;

namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Handles friend invitations via email or shareable link.
/// Both paths lead to automatic friendship acceptance.
/// </summary>
public interface IFriendInviteService
{
    /// <summary>
    /// Generates (or reuses a recent) invite link for <paramref name="inviterId"/>.
    /// Link-based invites are not email-targeted and can be shared freely.
    /// </summary>
    Task<FriendInviteLinkDto> CreateInviteLinkAsync(Guid inviterId, string baseUrl);

    /// <summary>
    /// Sends a friend invitation to <paramref name="email"/>.
    /// If a user with that email already exists, friendship is auto-accepted immediately.
    /// If not, an invite row is stored and an email is sent.
    /// </summary>
    Task<FriendInviteEmailResultDto> InviteByEmailAsync(Guid inviterId, string email, string baseUrl);

    /// <summary>
    /// Looks up the invite without consuming it — used to show the inviter's name
    /// on the accept page.
    /// </summary>
    Task<FriendInviteAcceptDto> GetInviteInfoAsync(string token);

    /// <summary>
    /// Redeems the invite: creates an accepted friendship between the inviter and
    /// <paramref name="acceptorId"/>. Idempotent — returns the result even if already accepted.
    /// Throws <see cref="KeyNotFoundException"/> when the token is unknown.
    /// Throws <see cref="InvalidOperationException"/> when expired or inviter == acceptor.
    /// </summary>
    Task<FriendInviteAcceptDto> AcceptInviteAsync(string token, Guid acceptorId);

    /// <summary>
    /// Revokes all pending link invites created by <paramref name="inviterId"/>.
    /// Email invites are unaffected (already sent).
    /// </summary>
    Task RevokeLinkInvitesAsync(Guid inviterId);
}
