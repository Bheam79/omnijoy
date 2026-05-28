namespace Omnijoy.Core.Models;

public class CompanyPage
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? CoverUrl { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public User CreatedByUser { get; set; } = null!;
    public ICollection<CompanyPageAdmin> Admins { get; set; } = [];
    public ICollection<CompanyPageFollow> Followers { get; set; } = [];
    public ICollection<Post> Posts { get; set; } = [];
    public ICollection<Event> Events { get; set; } = [];
}
