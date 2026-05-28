using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs.Chat;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

/// <summary>
/// Implements messenger business logic: conversations, messages, read-receipts,
/// media uploads.  SignalR delivery is handled by the API controllers.
/// </summary>
public class ChatService : IChatService
{
    private readonly OmnijoyDbContext _db;
    private readonly IMediaStorageService _storage;
    private readonly IPrivacyService _privacy;

    public ChatService(OmnijoyDbContext db, IMediaStorageService storage, IPrivacyService privacy)
    {
        _db = db;
        _storage = storage;
        _privacy = privacy;
    }

    // ── Conversations ─────────────────────────────────────────────────────────

    public async Task<ConversationDto[]> GetConversationsAsync(Guid userId)
    {
        // Fetch conversation IDs the user belongs to
        var conversationIds = await _db.ConversationParticipants
            .Where(cp => cp.UserId == userId)
            .Select(cp => cp.ConversationId)
            .ToListAsync();

        // Load conversations with participants and their last message
        var conversations = await _db.Conversations
            .AsNoTracking()
            .Include(c => c.Participants)
                .ThenInclude(p => p.User)
            .Where(c => conversationIds.Contains(c.Id))
            .ToListAsync();

        // Load last message per conversation (EF can't easily do this via Include + Take in a single query)
        var lastMessages = new Dictionary<Guid, Message?>();
        foreach (var convId in conversationIds)
        {
            lastMessages[convId] = await _db.Messages
                .AsNoTracking()
                .Include(m => m.Sender)
                .Include(m => m.Media)
                .Where(m => m.ConversationId == convId)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();
        }

        // Load LastReadAt for current user per conversation
        var participantMap = await _db.ConversationParticipants
            .Where(cp => cp.UserId == userId && conversationIds.Contains(cp.ConversationId))
            .ToDictionaryAsync(cp => cp.ConversationId, cp => cp.LastReadAt);

        // Build DTOs ordered by most-recent message
        var result = new List<ConversationDto>();
        foreach (var conv in conversations.OrderByDescending(c =>
            lastMessages.TryGetValue(c.Id, out var lm) && lm is not null
                ? lm.CreatedAt
                : c.CreatedAt))
        {
            var lastReadAt = participantMap.TryGetValue(conv.Id, out var lra) ? lra : null;
            var lastMsg = lastMessages.TryGetValue(conv.Id, out var lm2) ? lm2 : null;

            int unread = await _db.Messages
                .CountAsync(m =>
                    m.ConversationId == conv.Id &&
                    m.DeletedAt == null &&
                    m.SenderUserId != userId &&
                    (lastReadAt == null || m.CreatedAt > lastReadAt));

            result.Add(MapConversationDto(conv, lastMsg, unread));
        }

        return [.. result];
    }

    public async Task<ConversationDto> GetOrCreateDirectConversationAsync(Guid userId, Guid otherUserId)
    {
        if (userId == otherUserId)
            throw new ArgumentException("Cannot start a conversation with yourself.");

        // Find existing 1:1 conversation between these two users
        var existing = await _db.Conversations
            .Include(c => c.Participants)
                .ThenInclude(p => p.User)
            .Where(c =>
                c.Type == ConversationType.Direct &&
                c.Participants.Count == 2 &&
                c.Participants.Any(p => p.UserId == userId) &&
                c.Participants.Any(p => p.UserId == otherUserId))
            .FirstOrDefaultAsync();

        if (existing is not null)
        {
            var lastMsg = await GetLastMessageAsync(existing.Id);
            int unread = await CountUnreadAsync(existing.Id, userId);
            return MapConversationDto(existing, lastMsg, unread);
        }

        // Verify the other user exists
        _ = await _db.Users.FindAsync(otherUserId)
            ?? throw new KeyNotFoundException($"User {otherUserId} not found.");

        // Enforce WhoCanSendMessages privacy setting (only when starting a new conversation)
        if (!await _privacy.CanSendMessagesAsync(otherUserId, userId))
            throw new UnauthorizedAccessException("This user has restricted who can send them messages.");

        // Create new direct conversation
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.Direct,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Conversations.Add(conversation);

        _db.ConversationParticipants.Add(new ConversationParticipant
        {
            ConversationId = conversation.Id,
            UserId = userId,
            JoinedAt = DateTime.UtcNow,
        });
        _db.ConversationParticipants.Add(new ConversationParticipant
        {
            ConversationId = conversation.Id,
            UserId = otherUserId,
            JoinedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();

        // Reload with navigation properties
        var created = await _db.Conversations
            .AsNoTracking()
            .Include(c => c.Participants)
                .ThenInclude(p => p.User)
            .FirstAsync(c => c.Id == conversation.Id);

        return MapConversationDto(created, null, 0);
    }

    // ── Messages ──────────────────────────────────────────────────────────────

    public async Task<MessageDto[]> GetMessagesAsync(
        Guid conversationId, Guid requesterId, DateTime? before, int limit)
    {
        if (!await IsParticipantAsync(conversationId, requesterId))
            throw new UnauthorizedAccessException("You are not a participant in this conversation.");

        var query = _db.Messages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Include(m => m.Media)
            .Where(m => m.ConversationId == conversationId);

        if (before.HasValue)
            query = query.Where(m => m.CreatedAt < before.Value);

        var messages = await query
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync();

        // Return ascending (oldest first) for chat display
        return messages
            .OrderBy(m => m.CreatedAt)
            .Select(MapMessageDto)
            .ToArray();
    }

    public async Task<MessageDto> SendMessageAsync(
        Guid conversationId,
        Guid senderId,
        string? content,
        IReadOnlyList<MediaUploadItem>? mediaItems)
    {
        if (!await IsParticipantAsync(conversationId, senderId))
            throw new UnauthorizedAccessException("You are not a participant in this conversation.");

        if (string.IsNullOrWhiteSpace(content) && (mediaItems is null || mediaItems.Count == 0))
            throw new ArgumentException("Message must have content or an attached file.");

        // Determine message type from the uploaded file (if any)
        var msgType = MessageType.Text;
        if (mediaItems is { Count: > 0 })
        {
            var first = mediaItems[0];
            var ct  = first.ContentType.ToLowerInvariant();
            var ext = Path.GetExtension(first.FileName).ToLowerInvariant();

            if (ct.StartsWith("image/") || ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp")
                msgType = MessageType.Image;
            else if (ct.StartsWith("video/") || ext is ".mp4" or ".webm" or ".mov" or ".avi" or ".mkv")
                msgType = MessageType.Video;
            else
                msgType = MessageType.File;
        }

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderUserId = senderId,
            Content = string.IsNullOrWhiteSpace(content) ? null : content.Trim(),
            MessageType = msgType,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Messages.Add(message);

        // Upload first media item (chat allows one attachment per message)
        if (mediaItems is { Count: > 0 })
        {
            var item = mediaItems[0];
            var folder = msgType switch
            {
                MessageType.Image => "messages/images",
                MessageType.Video => "messages/videos",
                _                 => "messages/files",
            };
            var mediaType = msgType switch
            {
                MessageType.Image => MediaType.Image,
                MessageType.Video => MediaType.Video,
                _                 => MediaType.File,
            };

            var url = await _storage.StoreAsync(item.Content, item.FileName, folder);

            _db.MessageMedia.Add(new MessageMedia
            {
                Id = Guid.NewGuid(),
                MessageId = message.Id,
                MediaType = mediaType,
                Url = url,
                FileName = item.FileName,
                FileSizeBytes = item.Content.CanSeek ? item.Content.Length : null,
            });
        }

        await _db.SaveChangesAsync();

        return await LoadMessageDtoAsync(message.Id);
    }

    public async Task<MessageDto> DeleteMessageAsync(Guid messageId, Guid requesterId)
    {
        var message = await _db.Messages
            .Include(m => m.Sender)
            .Include(m => m.Media)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.DeletedAt == null)
            ?? throw new KeyNotFoundException($"Message {messageId} not found.");

        if (message.SenderUserId != requesterId)
            throw new UnauthorizedAccessException("You can only delete your own messages.");

        message.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return MapMessageDto(message);
    }

    public async Task MarkConversationReadAsync(Guid conversationId, Guid userId)
    {
        var participant = await _db.ConversationParticipants
            .FirstOrDefaultAsync(cp =>
                cp.ConversationId == conversationId && cp.UserId == userId);

        if (participant is null) return;

        participant.LastReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<bool> IsParticipantAsync(Guid conversationId, Guid userId)
        => await _db.ConversationParticipants
            .AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId);

    public Task<Guid[]> GetParticipantUserIdsAsync(Guid conversationId)
        => _db.ConversationParticipants
            .AsNoTracking()
            .Where(cp => cp.ConversationId == conversationId)
            .Select(cp => cp.UserId)
            .ToArrayAsync();

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<Message?> GetLastMessageAsync(Guid conversationId)
        => await _db.Messages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Include(m => m.Media)
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync();

    private async Task<int> CountUnreadAsync(Guid conversationId, Guid userId)
    {
        var lastReadAt = await _db.ConversationParticipants
            .Where(cp => cp.ConversationId == conversationId && cp.UserId == userId)
            .Select(cp => cp.LastReadAt)
            .FirstOrDefaultAsync();

        return await _db.Messages.CountAsync(m =>
            m.ConversationId == conversationId &&
            m.DeletedAt == null &&
            m.SenderUserId != userId &&
            (lastReadAt == null || m.CreatedAt > lastReadAt));
    }

    private async Task<MessageDto> LoadMessageDtoAsync(Guid messageId)
    {
        var msg = await _db.Messages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Include(m => m.Media)
            .FirstAsync(m => m.Id == messageId);
        return MapMessageDto(msg);
    }

    // ── Mappers ───────────────────────────────────────────────────────────────

    private static ConversationDto MapConversationDto(
        Conversation conv, Message? lastMsg, int unreadCount)
    {
        var participants = conv.Participants
            .Select(p => new ChatUserDto(p.User.Id, p.User.DisplayName, p.User.AvatarUrl))
            .ToArray();

        return new ConversationDto(
            Id:           conv.Id,
            Type:         conv.Type.ToString(),
            Participants: participants,
            LastMessage:  lastMsg is not null ? MapMessageDto(lastMsg) : null,
            UnreadCount:  unreadCount,
            CreatedAt:    conv.CreatedAt
        );
    }

    private static MessageDto MapMessageDto(Message msg)
    {
        var sender    = new ChatUserDto(msg.Sender.Id, msg.Sender.DisplayName, msg.Sender.AvatarUrl);
        var isDeleted = msg.DeletedAt.HasValue;

        MessageMediaItemDto? media = null;
        if (!isDeleted)
        {
            var m = msg.Media.FirstOrDefault();
            if (m is not null)
                media = new MessageMediaItemDto(
                    m.Id, m.MediaType.ToString(), m.Url, m.FileName, m.FileSizeBytes);
        }

        return new MessageDto(
            Id:          msg.Id,
            ConversationId: msg.ConversationId,
            Sender:      sender,
            Content:     isDeleted ? null : msg.Content,
            MessageType: isDeleted ? "Text" : msg.MessageType.ToString(),
            Media:       media,
            CreatedAt:   msg.CreatedAt,
            IsDeleted:   isDeleted
        );
    }
}
