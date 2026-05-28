using Omnijoy.Core.DTOs.Chat;
using Omnijoy.Core.DTOs.Posts;

namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Business logic for the messenger: conversations, message CRUD,
/// read-receipts, and media upload.  All real-time delivery is done
/// by the controllers via IHubContext&lt;ChatHub&gt;.
/// </summary>
public interface IChatService
{
    /// <summary>Returns all conversations for the user, sorted by most-recent message.</summary>
    Task<ConversationDto[]> GetConversationsAsync(Guid userId);

    /// <summary>
    /// Returns the existing direct conversation between two users,
    /// or creates a new one if none exists.
    /// </summary>
    Task<ConversationDto> GetOrCreateDirectConversationAsync(Guid userId, Guid otherUserId);

    /// <summary>
    /// Returns up to <paramref name="limit"/> messages before <paramref name="before"/>
    /// (cursor-based pagination).  Messages are returned in ascending order (oldest first).
    /// </summary>
    Task<MessageDto[]> GetMessagesAsync(
        Guid conversationId, Guid requesterId, DateTime? before, int limit);

    /// <summary>Sends a message (with optional media upload) to a conversation.</summary>
    Task<MessageDto> SendMessageAsync(
        Guid conversationId,
        Guid senderId,
        string? content,
        IReadOnlyList<MediaUploadItem>? mediaItems);

    /// <summary>Soft-deletes a message.  Only the sender may delete.</summary>
    Task<MessageDto> DeleteMessageAsync(Guid messageId, Guid requesterId);

    /// <summary>Updates the user's LastReadAt to now, resetting the unread count.</summary>
    Task MarkConversationReadAsync(Guid conversationId, Guid userId);

    /// <summary>Returns true when the given user is a participant in the conversation.</summary>
    Task<bool> IsParticipantAsync(Guid conversationId, Guid userId);

    /// <summary>
    /// Returns the user IDs of every participant in the conversation
    /// (used to fan out MessageReceived notifications via SignalR).
    /// </summary>
    Task<Guid[]> GetParticipantUserIdsAsync(Guid conversationId);
}
