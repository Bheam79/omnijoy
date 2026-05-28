using Omnijoy.Core.DTOs.Reports;

namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Business logic for submitting, listing, and moderating content reports.
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Submits a new report from the given reporter.
    /// Throws <see cref="ArgumentException"/> if the target type or reason is invalid.
    /// Throws <see cref="KeyNotFoundException"/> if the target does not exist.
    /// Throws <see cref="InvalidOperationException"/> if the reporter already filed a report
    /// against the same target.
    /// </summary>
    Task<ReportDto> SubmitReportAsync(Guid reporterId, SubmitReportRequest request);

    /// <summary>
    /// Returns a paginated, optionally-filtered list of reports (admin only).
    /// <paramref name="status"/> and <paramref name="targetType"/> are optional filter strings.
    /// </summary>
    Task<ReportListResult> ListReportsAsync(string? status, string? targetType, int page, int pageSize);

    /// <summary>
    /// Updates the status of an existing report (admin only).
    /// Throws <see cref="KeyNotFoundException"/> if the report does not exist.
    /// Throws <see cref="ArgumentException"/> if the status value is invalid.
    /// </summary>
    Task<ReportDto> UpdateStatusAsync(Guid reportId, UpdateReportStatusRequest request);
}
