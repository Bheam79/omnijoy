using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Models;

public class FamilyRelation
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid RelatedUserId { get; set; }
    public FamilyRelationType RelationType { get; set; }
    public FamilyRelationSubtype Subtype { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public User RelatedUser { get; set; } = null!;
}
