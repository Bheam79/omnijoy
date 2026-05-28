namespace Omnijoy.Core.DTOs.Chat;

// ── Shared sub-records ─────────────────────────────────────────────────────────

/// <summary>Minimal user info embedded in chat responses.</summary>
public record ChatUserDto(Guid Id, string DisplayName, string? AvatarUrl);

/// <summary>Media attachment embedded in a message.</summary>
public record MessageMediaItemDto(
    Guid Id,
    /// <summary>"Image" | "Video" | "File"</summary>
    string MediaType,
    string Url,
    string? FileName,
    long? FileSizeBytes
);

/// <summary>Open Graph preview returned by GET /api/meta-preview.</summary>
public record OgPreviewDto(
    string Url,
    string? Title,
    string? Description,
    string? ImageUrl,
    string? SiteName
);

// ── Message ───────────────────────────────────────────────────────────────────

public record MessageDto(
    Guid Id,
    Guid ConversationId,
    ChatUserDto Sender,
    string? Content,
    /// <summary>"Text" | "Image" | "Video" | "File"</summary>
    string MessageType,
    MessageMediaItemDto? Media,
    DateTime CreatedAt,
    bool IsDeleted
);

// ── Conversation ───────────────────────────────────────────────────────────────

public record ConversationDto(
    Guid Id,
    /// <summary>"Direct" | "Group"</summary>
    string Type,
    ChatUserDto[] Participants,
    MessageDto? LastMessage,
    int UnreadCount,
    DateTime CreatedAt
);
