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
    /// Updates the status of an existing report (admin/moderator only).
    /// Writes a corresponding entry to the moderation audit log (
    /// <c>ReviewReport</c> for <c>Reviewed</c>, <c>DismissReport</c> for
    /// <c>Dismissed</c>) under <paramref name="actorId"/>.
    /// Throws <see cref="KeyNotFoundException"/> if the report does not exist.
    /// Throws <see cref="ArgumentException"/> if the status value is invalid.
    /// </summary>
    Task<ReportDto> UpdateStatusAsync(Guid actorId, Guid reportId, UpdateReportStatusRequest request);
}
