using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs.Friends;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

public class FriendInviteService : IFriendInviteService
{
    private const int InviteExpiryDays = 30;

    private readonly OmnijoyDbContext _db;
    private readonly IEmailService _email;

    public FriendInviteService(OmnijoyDbContext db, IEmailService email)
    {
        _db    = db;
        _email = email;
    }

    // ── Create invite link ────────────────────────────────────────────────────

    public async Task<FriendInviteLinkDto> CreateInviteLinkAsync(Guid inviterId, string baseUrl)
    {
        // Reuse an existing non-expired, non-email link invite if one exists.
        var existing = await _db.FriendInvites
            .Where(fi => fi.InviterId == inviterId &&
                         fi.Email == null &&
                         fi.Status == FriendInviteStatus.Pending &&
                         fi.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(fi => fi.CreatedAt)
            .FirstOrDefaultAsync();

        if (existing is not null)
            return ToLinkDto(existing, baseUrl);

        var invite = CreateInvite(inviterId, email: null);
        _db.FriendInvites.Add(invite);
        await _db.SaveChangesAsync();

        return ToLinkDto(invite, baseUrl);
    }

    // ── Invite by email ───────────────────────────────────────────────────────

    public async Task<FriendInviteEmailResultDto> InviteByEmailAsync(
        Guid inviterId, string email, string baseUrl)
    {
        email = email.Trim().ToLowerInvariant();

        var inviter = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == inviterId)
            ?? throw new KeyNotFoundException("Inviter not found.");

        if (inviter.Email.Equals(email, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("You cannot invite yourself.");

        // Does the target already have an account?
        var target = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email);

        if (target is not null)
        {
            // Auto-accept friendship immediately (same logic as FriendService).
            await AutoAcceptFriendshipAsync(inviterId, target.Id);
            return new FriendInviteEmailResultDto("accepted", target.DisplayName);
        }

        // Check for a duplicate pending email invite (don't double-send).
        var duplicatePending = await _db.FriendInvites
            .AnyAsync(fi => fi.InviterId == inviterId &&
                            fi.Email == email &&
                            fi.Status == FriendInviteStatus.Pending &&
                            fi.ExpiresAt > DateTime.UtcNow);

        if (!duplicatePending)
        {
            var invite = CreateInvite(inviterId, email);
            _db.FriendInvites.Add(invite);
            await _db.SaveChangesAsync();

            var inviteUrl = $"{baseUrl.TrimEnd('/')}/invite/{invite.Token}";
            await _email.SendFriendInviteEmailAsync(email, inviter.DisplayName, inviteUrl);
        }

        return new FriendInviteEmailResultDto("invited", null);
    }

    // ── Get invite info (preview before accepting) ────────────────────────────

    public async Task<FriendInviteAcceptDto> GetInviteInfoAsync(string token)
    {
        var invite = await _db.FriendInvites
            .AsNoTracking()
            .Include(fi => fi.Inviter)
            .FirstOrDefaultAsync(fi => fi.Token == token)
            ?? throw new KeyNotFoundException("Invite not found.");

        return new FriendInviteAcceptDto(
            invite.Inviter.DisplayName,
            invite.Inviter.AvatarUrl,
            invite.InviterId);
    }

    // ── Accept invite ─────────────────────────────────────────────────────────

    public async Task<FriendInviteAcceptDto> AcceptInviteAsync(string token, Guid acceptorId)
    {
        var invite = await _db.FriendInvites
            .Include(fi => fi.Inviter)
            .FirstOrDefaultAsync(fi => fi.Token == token)
            ?? throw new KeyNotFoundException("Invite not found.");

        if (invite.InviterId == acceptorId)
            throw new InvalidOperationException("You cannot accept your own invite.");

        if (invite.Status == FriendInviteStatus.Revoked)
            throw new InvalidOperationException("This invite has been revoked.");

        if (invite.ExpiresAt < DateTime.UtcNow && invite.Status != FriendInviteStatus.Accepted)
            throw new InvalidOperationException("This invite has expired.");

        // Idempotent — if already accepted, just return info.
        if (invite.Status != FriendInviteStatus.Accepted)
        {
            await AutoAcceptFriendshipAsync(invite.InviterId, acceptorId);

            invite.Status = FriendInviteStatus.Accepted;
            invite.AcceptedByUserId = acceptorId;
            invite.AcceptedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return new FriendInviteAcceptDto(
            invite.Inviter.DisplayName,
            invite.Inviter.AvatarUrl,
            invite.InviterId);
    }

    // ── Revoke link invites ───────────────────────────────────────────────────

    public async Task RevokeLinkInvitesAsync(Guid inviterId)
    {
        var pendingLinks = await _db.FriendInvites
            .Where(fi => fi.InviterId == inviterId &&
                         fi.Email == null &&
                         fi.Status == FriendInviteStatus.Pending)
            .ToListAsync();

        foreach (var inv in pendingLinks)
        {
            inv.Status = FriendInviteStatus.Revoked;
        }

        if (pendingLinks.Count > 0)
            await _db.SaveChangesAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static FriendInvite CreateInvite(Guid inviterId, string? email)
    {
        var now = DateTime.UtcNow;
        return new FriendInvite
        {
            Id        = Guid.NewGuid(),
            InviterId = inviterId,
            Token     = GenerateToken(),
            Email     = email,
            Status    = FriendInviteStatus.Pending,
            ExpiresAt = now.AddDays(InviteExpiryDays),
            CreatedAt = now,
        };
    }

    private static string GenerateToken()
    {
        // 32 random bytes → 43-char base64url (no padding)
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static FriendInviteLinkDto ToLinkDto(FriendInvite invite, string baseUrl)
    {
        var url = $"{baseUrl.TrimEnd('/')}/invite/{invite.Token}";
        return new FriendInviteLinkDto(invite.Token, url, invite.ExpiresAt);
    }

    /// <summary>
    /// Creates an Accepted friendship between two users.
    /// Handles all existing-record states (pending, declined, already friends).
    /// </summary>
    private async Task AutoAcceptFriendshipAsync(Guid requesterId, Guid addresseeId)
    {
        var existing = await _db.Friends.FirstOrDefaultAsync(f =>
            (f.RequesterId == requesterId && f.AddresseeId == addresseeId) ||
            (f.RequesterId == addresseeId && f.AddresseeId == requesterId));

        if (existing is not null)
        {
            if (existing.Status == FriendStatus.Accepted)
                return; // Already friends — nothing to do.

            if (existing.Status == FriendStatus.Blocked)
                return; // Respect existing blocks.

            existing.Status = FriendStatus.Accepted;
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return;
        }

        _db.Friends.Add(new Friend
        {
            Id          = Guid.NewGuid(),
            RequesterId = requesterId,
            AddresseeId = addresseeId,
            Status      = FriendStatus.Accepted,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }
}
