using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Models;

public class CompanyPageAdmin
{
    public Guid CompanyPageId { get; set; }
    public Guid UserId { get; set; }
    public CompanyPageRole Role { get; set; } = CompanyPageRole.Editor;

    // Navigation properties
    public CompanyPage CompanyPage { get; set; } = null!;
    public User User { get; set; } = null!;
}
