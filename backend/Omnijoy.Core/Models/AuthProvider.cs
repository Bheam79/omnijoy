using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Models;

public class AuthProvider
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public AuthProviderType Provider { get; set; }
    public string? ProviderUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation property
    public User User { get; set; } = null!;
}
