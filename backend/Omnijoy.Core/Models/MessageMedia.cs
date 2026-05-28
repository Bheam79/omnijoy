using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Models;

public class MessageMedia
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public MediaType MediaType { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public long? FileSizeBytes { get; set; }

    // Navigation property
    public Message Message { get; set; } = null!;
}
