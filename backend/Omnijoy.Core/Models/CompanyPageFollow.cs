namespace Omnijoy.Core.Models;

public class CompanyPageFollow
{
    public Guid CompanyPageId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public CompanyPage CompanyPage { get; set; } = null!;
    public User User { get; set; } = null!;
}
