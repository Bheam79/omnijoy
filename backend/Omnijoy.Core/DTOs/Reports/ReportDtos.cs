namespace Omnijoy.Core.DTOs.Reports;

// ── Request payloads ──────────────────────────────────────────────────────────

/// <summary>
/// JSON body for POST /api/reports.
/// </summary>
public record SubmitReportRequest(
    /// <summary>"Post" | "User" | "Comment"</summary>
    string TargetType,
    Guid TargetId,
    /// <summary>"Spam" | "Harassment" | "Misinformation" | "Nudity" | "Violence" | "Other"</summary>
    string Reason,
    string? Notes
);

/// <summary>
/// JSON body for PATCH /api/admin/reports/{id}.
/// </summary>
public record UpdateReportStatusRequest(
    /// <summary>"Reviewed" | "Dismissed"</summary>
    string Status
);

// ── Response DTOs ─────────────────────────────────────────────────────────────

public record ReportDto(
    Guid Id,
    Guid ReporterId,
    string ReporterName,
    string TargetType,
    Guid TargetId,
    string Reason,
    string? Notes,
    string Status,
    DateTime CreatedAt
);

public record ReportListResult(
    ReportDto[] Items,
    int Page,
    int PageSize,
    bool HasMore
);
