using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Core.Models;

public class Report
{
    public Guid Id { get; set; }
    public Guid ReporterId { get; set; }
    public ReportTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public ReportReason Reason { get; set; }
    public string? Notes { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Pending;
    public DateTime CreatedAt { get; set; }

    // Navigation
    public User Reporter { get; set; } = null!;
}
