using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Models;

public class OtpCode
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsUsed { get; set; }

    /// <summary>
    /// Discriminates between OTP flows so Login codes and PasswordReset codes
    /// can never cross-pollinate. Defaults to <see cref="OtpPurpose.Login"/> so
    /// existing rows back-fill cleanly without a data migration.
    /// </summary>
    public OtpPurpose Purpose { get; set; } = OtpPurpose.Login;

    /// <summary>
    /// Tracks the number of wrong-code submissions against this record.
    /// Once it reaches the configured cap (5) the record is treated as
    /// exhausted even if <see cref="IsUsed"/> is still false.
    /// </summary>
    public int AttemptCount { get; set; }
}
