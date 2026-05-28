using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Models;

public class PostMedia
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public MediaType MediaType { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public int Order { get; set; }

    // Navigation property
    public Post Post { get; set; } = null!;
}
