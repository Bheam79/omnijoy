using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs.Friends;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

public class FriendService : IFriendService
{
    private readonly OmnijoyDbContext _db;

    public FriendService(OmnijoyDbContext db) => _db = db;

    // ── Send request ──────────────────────────────────────────────────────────

    public async Task<FriendRequestDto> SendRequestAsync(Guid requesterId, Guid addresseeId)
    {
        if (requesterId == addresseeId)
            throw new ArgumentException("You cannot send a friend request to yourself.");

        // Ensure target user exists
        var addressee = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == addresseeId)
            ?? throw new KeyNotFoundException($"User {addresseeId} not found.");

        var existing = await FindRecordAsync(requesterId, addresseeId);

        if (existing is not null)
        {
            switch (existing.Status)
            {
                case FriendStatus.Accepted:
                    throw new InvalidOperationException("You are already friends.");

                case FriendStatus.Blocked when existing.RequesterId == requesterId:
                    throw new InvalidOperationException("You have blocked this user.");

                case FriendStatus.Blocked:
                    throw new InvalidOperationException("This user is not available.");

                case FriendStatus.Pending when existing.RequesterId == requesterId:
                    throw new InvalidOperationException("Friend request already sent.");

                case FriendStatus.Pending:
                    // They already sent a request to us — auto-accept
                    existing.Status = FriendStatus.Accepted;
                    existing.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                    // Return as if we accepted (we'll treat it as a request sent from caller's view)
                    return new FriendRequestDto(existing.Id, new FriendUserDto(addressee.Id, addressee.DisplayName, addressee.AvatarUrl), existing.CreatedAt);

                case FriendStatus.Declined:
                    // Re-send after being declined
                    existing.Status = FriendStatus.Pending;
                    existing.RequesterId = requesterId;
                    existing.AddresseeId = addresseeId;
                    existing.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                    return new FriendRequestDto(existing.Id, new FriendUserDto(addressee.Id, addressee.DisplayName, addressee.AvatarUrl), existing.CreatedAt);
            }
        }

        var record = new Friend
        {
            Id = Guid.NewGuid(),
            RequesterId = requesterId,
            AddresseeId = addresseeId,
            Status = FriendStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Friends.Add(record);
        await _db.SaveChangesAsync();

        return new FriendRequestDto(record.Id, new FriendUserDto(addressee.Id, addressee.DisplayName, addressee.AvatarUrl), record.CreatedAt);
    }

    // ── Accept ────────────────────────────────────────────────────────────────

    public async Task<FriendDto> AcceptRequestAsync(Guid userId, Guid requesterId)
    {
        var record = await _db.Friends
            .Include(f => f.Requester)
            .FirstOrDefaultAsync(f =>
                f.RequesterId == requesterId &&
                f.AddresseeId == userId &&
                f.Status == FriendStatus.Pending)
            ?? throw new KeyNotFoundException("No pending friend request found from that user.");

        record.Status = FriendStatus.Accepted;
        record.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var mutual = await MutualFriendCountAsync(userId, requesterId);

        return new FriendDto(
            record.Id,
            new FriendUserDto(record.Requester.Id, record.Requester.DisplayName, record.Requester.AvatarUrl),
            mutual,
            record.UpdatedAt,
            null
        );
    }

    // ── Decline ───────────────────────────────────────────────────────────────

    public async Task DeclineRequestAsync(Guid userId, Guid requesterId)
    {
        var record = await _db.Friends.FirstOrDefaultAsync(f =>
            f.RequesterId == requesterId &&
            f.AddresseeId == userId &&
            f.Status == FriendStatus.Pending)
            ?? throw new KeyNotFoundException("No pending friend request found from that user.");

        record.Status = FriendStatus.Declined;
        record.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    public async Task CancelRequestAsync(Guid userId, Guid addresseeId)
    {
        var record = await _db.Friends.FirstOrDefaultAsync(f =>
            f.RequesterId == userId &&
            f.AddresseeId == addresseeId &&
            f.Status == FriendStatus.Pending)
            ?? throw new KeyNotFoundException("No pending friend request to cancel.");

        _db.Friends.Remove(record);
        await _db.SaveChangesAsync();
    }

    // ── Remove friend ─────────────────────────────────────────────────────────

    public async Task RemoveFriendAsync(Guid userId, Guid otherUserId)
    {
        var record = await FindRecordAsync(userId, otherUserId);
        if (record is null || record.Status != FriendStatus.Accepted)
            throw new KeyNotFoundException("Friendship not found.");

        _db.Friends.Remove(record);
        await _db.SaveChangesAsync();
    }

    // ── Block ─────────────────────────────────────────────────────────────────

    public async Task BlockUserAsync(Guid userId, Guid targetId)
    {
        if (userId == targetId)
            throw new ArgumentException("Cannot block yourself.");

        var existing = await FindRecordAsync(userId, targetId);

        if (existing is not null)
        {
            if (existing.Status == FriendStatus.Blocked && existing.RequesterId == userId)
                return; // already blocked by this user

            if (existing.RequesterId != userId)
            {
                // Record is in the wrong direction — replace it
                _db.Friends.Remove(existing);
                await _db.SaveChangesAsync();
                existing = null;
            }
        }

        if (existing is null)
        {
            _db.Friends.Add(new Friend
            {
                Id = Guid.NewGuid(),
                RequesterId = userId,
                AddresseeId = targetId,
                Status = FriendStatus.Blocked,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.Status = FriendStatus.Blocked;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    // ── Unblock ───────────────────────────────────────────────────────────────

    public async Task UnblockUserAsync(Guid userId, Guid targetId)
    {
        var record = await _db.Friends.FirstOrDefaultAsync(f =>
            f.RequesterId == userId &&
            f.AddresseeId == targetId &&
            f.Status == FriendStatus.Blocked)
            ?? throw new KeyNotFoundException("No active block found for this user.");

        _db.Friends.Remove(record);
        await _db.SaveChangesAsync();
    }

    // ── Get friends (paginated) ────────────────────────────────────────────────

    public async Task<FriendsPageResult> GetFriendsAsync(Guid userId, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = _db.Friends
            .AsNoTracking()
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .Where(f => f.Status == FriendStatus.Accepted &&
                        (f.RequesterId == userId || f.AddresseeId == userId))
            .OrderBy(f => f.UpdatedAt);

        var total = await query.CountAsync();
        var records = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var items = new List<FriendDto>(records.Count);
        foreach (var r in records)
        {
            var friend = r.RequesterId == userId ? r.Addressee : r.Requester;
            var mutual = await MutualFriendCountAsync(userId, friend.Id);
            var relation = await GetFamilyRelationAsync(userId, friend.Id);

            items.Add(new FriendDto(
                r.Id,
                new FriendUserDto(friend.Id, friend.DisplayName, friend.AvatarUrl),
                mutual,
                r.UpdatedAt,
                relation
            ));
        }

        return new FriendsPageResult(items.ToArray(), page, pageSize, (page * pageSize) < total);
    }

    // ── Pending incoming requests ─────────────────────────────────────────────

    public async Task<FriendRequestDto[]> GetPendingRequestsAsync(Guid userId)
    {
        return await _db.Friends
            .AsNoTracking()
            .Include(f => f.Requester)
            .Where(f => f.AddresseeId == userId && f.Status == FriendStatus.Pending)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new FriendRequestDto(
                f.Id,
                new FriendUserDto(f.Requester.Id, f.Requester.DisplayName, f.Requester.AvatarUrl),
                f.CreatedAt
            ))
            .ToArrayAsync();
    }

    // ── Sent pending requests ─────────────────────────────────────────────────

    public async Task<FriendRequestDto[]> GetSentRequestsAsync(Guid userId)
    {
        return await _db.Friends
            .AsNoTracking()
            .Include(f => f.Addressee)
            .Where(f => f.RequesterId == userId && f.Status == FriendStatus.Pending)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new FriendRequestDto(
                f.Id,
                new FriendUserDto(f.Addressee.Id, f.Addressee.DisplayName, f.Addressee.AvatarUrl),
                f.CreatedAt
            ))
            .ToArrayAsync();
    }

    // ── Suggestions ───────────────────────────────────────────────────────────

    public async Task<FriendSuggestionDto[]> GetSuggestionsAsync(Guid userId, int limit = 10)
    {
        // Get accepted friend IDs
        var friendIds = await _db.Friends
            .AsNoTracking()
            .Where(f => f.Status == FriendStatus.Accepted &&
                        (f.RequesterId == userId || f.AddresseeId == userId))
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
            .ToListAsync();

        if (friendIds.Count == 0) return [];

        // Get IDs of users already in any relationship
        var knownIds = await _db.Friends
            .AsNoTracking()
            .Where(f => f.RequesterId == userId || f.AddresseeId == userId)
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
            .ToListAsync();

        knownIds.Add(userId);

        // Friends-of-friends who are not already known
        var candidates = await _db.Friends
            .AsNoTracking()
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .Where(f => f.Status == FriendStatus.Accepted &&
                        (friendIds.Contains(f.RequesterId) || friendIds.Contains(f.AddresseeId)))
            .ToListAsync();

        var suggestions = candidates
            .SelectMany(f => new[] { f.Requester, f.Addressee })
            .Where(u => !knownIds.Contains(u.Id))
            .GroupBy(u => u.Id)
            .Select(g => new FriendSuggestionDto(
                new FriendUserDto(g.Key, g.First().DisplayName, g.First().AvatarUrl),
                g.Count()
            ))
            .OrderByDescending(s => s.MutualFriendCount)
            .Take(limit)
            .ToArray();

        return suggestions;
    }

    // ── Status ────────────────────────────────────────────────────────────────

    public async Task<FriendStatusDto> GetFriendStatusAsync(Guid userId, Guid otherId)
    {
        if (userId == otherId)
            return new FriendStatusDto("Self");

        var record = await FindRecordAsync(userId, otherId);

        if (record is null)
            return new FriendStatusDto("None");

        string status = record.Status switch
        {
            FriendStatus.Accepted => "Accepted",
            FriendStatus.Blocked when record.RequesterId == userId => "BlockedByMe",
            FriendStatus.Blocked => "BlockedByThem",
            FriendStatus.Pending when record.RequesterId == userId => "RequestSent",
            FriendStatus.Pending => "RequestReceived",
            FriendStatus.Declined => "None",
            _ => "None"
        };

        return new FriendStatusDto(status);
    }

    // ── Pending count (nav badge) ─────────────────────────────────────────────

    public async Task<int> GetPendingRequestCountAsync(Guid userId)
    {
        return await _db.Friends.CountAsync(f =>
            f.AddresseeId == userId && f.Status == FriendStatus.Pending);
    }

    // ── Family relations ──────────────────────────────────────────────────────

    public async Task<FamilyRelationDto?> SetFamilyRelationAsync(
        Guid userId,
        Guid relatedUserId,
        SetFamilyRelationRequest request)
    {
        // Must be friends
        var friendship = await FindRecordAsync(userId, relatedUserId);
        if (friendship is null || friendship.Status != FriendStatus.Accepted)
            throw new InvalidOperationException("You must be friends to set a family relation.");

        var existing = await _db.FamilyRelations
            .FirstOrDefaultAsync(fr => fr.UserId == userId && fr.RelatedUserId == relatedUserId);

        // Clear relation if RelationType is null/empty
        if (string.IsNullOrWhiteSpace(request.RelationType))
        {
            if (existing is not null)
            {
                _db.FamilyRelations.Remove(existing);
                await _db.SaveChangesAsync();
            }
            return null;
        }

        if (!Enum.TryParse<FamilyRelationType>(request.RelationType, ignoreCase: true, out var relType))
            throw new ArgumentException($"Invalid RelationType: '{request.RelationType}'.");

        FamilyRelationSubtype subtype = FamilyRelationSubtype.Son; // default
        if (!string.IsNullOrWhiteSpace(request.Subtype) &&
            !Enum.TryParse(request.Subtype, ignoreCase: true, out subtype))
            throw new ArgumentException($"Invalid Subtype: '{request.Subtype}'.");

        if (existing is null)
        {
            existing = new FamilyRelation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                RelatedUserId = relatedUserId,
                RelationType = relType,
                Subtype = subtype,
                CreatedAt = DateTime.UtcNow,
            };
            _db.FamilyRelations.Add(existing);
        }
        else
        {
            existing.RelationType = relType;
            existing.Subtype = subtype;
        }

        await _db.SaveChangesAsync();
        return new FamilyRelationDto(existing.RelationType.ToString(), existing.Subtype.ToString());
    }

    public async Task<FamilyRelationDto?> GetFamilyRelationAsync(Guid userId, Guid relatedUserId)
    {
        var rel = await _db.FamilyRelations
            .AsNoTracking()
            .FirstOrDefaultAsync(fr => fr.UserId == userId && fr.RelatedUserId == relatedUserId);

        return rel is null ? null : new FamilyRelationDto(rel.RelationType.ToString(), rel.Subtype.ToString());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Finds a Friend record between two users regardless of direction.</summary>
    private async Task<Friend?> FindRecordAsync(Guid a, Guid b)
    {
        return await _db.Friends.FirstOrDefaultAsync(f =>
            (f.RequesterId == a && f.AddresseeId == b) ||
            (f.RequesterId == b && f.AddresseeId == a));
    }

    private async Task<int> MutualFriendCountAsync(Guid a, Guid b)
    {
        var aFriends = await _db.Friends
            .AsNoTracking()
            .Where(f => f.Status == FriendStatus.Accepted &&
                        (f.RequesterId == a || f.AddresseeId == a))
            .Select(f => f.RequesterId == a ? f.AddresseeId : f.RequesterId)
            .ToListAsync();

        var bFriends = await _db.Friends
            .AsNoTracking()
            .Where(f => f.Status == FriendStatus.Accepted &&
                        (f.RequesterId == b || f.AddresseeId == b))
            .Select(f => f.RequesterId == b ? f.AddresseeId : f.RequesterId)
            .ToListAsync();

        return aFriends.Intersect(bFriends).Count();
    }
}
