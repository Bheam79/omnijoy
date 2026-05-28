using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs.Reports;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly OmnijoyDbContext _db;

    public ReportService(OmnijoyDbContext db)
    {
        _db = db;
    }

    // ── Submit ────────────────────────────────────────────────────────────────

    public async Task<ReportDto> SubmitReportAsync(Guid reporterId, SubmitReportRequest request)
    {
        if (!Enum.TryParse<ReportTargetType>(request.TargetType, ignoreCase: true, out var targetType))
            throw new ArgumentException($"Invalid TargetType: '{request.TargetType}'.");

        if (!Enum.TryParse<ReportReason>(request.Reason, ignoreCase: true, out var reason))
            throw new ArgumentException($"Invalid Reason: '{request.Reason}'.");

        // Verify the target exists
        await VerifyTargetExistsAsync(targetType, request.TargetId);

        // Prevent duplicate reports from the same user on the same target
        var duplicate = await _db.Reports.AnyAsync(r =>
            r.ReporterId == reporterId &&
            r.TargetType == targetType &&
            r.TargetId == request.TargetId);

        if (duplicate)
            throw new InvalidOperationException("You have already reported this content.");

        var report = new Report
        {
            Id = Guid.NewGuid(),
            ReporterId = reporterId,
            TargetType = targetType,
            TargetId = request.TargetId,
            Reason = reason,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Status = ReportStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };

        _db.Reports.Add(report);
        await _db.SaveChangesAsync();

        // Load reporter for DTO projection
        var reporter = await _db.Users.AsNoTracking()
            .FirstAsync(u => u.Id == reporterId);

        return ToDto(report, reporter.DisplayName);
    }

    // ── List (admin) ──────────────────────────────────────────────────────────

    public async Task<ReportListResult> ListReportsAsync(
        string? status,
        string? targetType,
        int page,
        int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Reports
            .AsNoTracking()
            .Include(r => r.Reporter)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<ReportStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            query = query.Where(r => r.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(targetType) &&
            Enum.TryParse<ReportTargetType>(targetType, ignoreCase: true, out var parsedTargetType))
        {
            query = query.Where(r => r.TargetType == parsedTargetType);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ReportListResult(
            Items: items.Select(r => ToDto(r, r.Reporter.DisplayName)).ToArray(),
            Page: page,
            PageSize: pageSize,
            HasMore: page * pageSize < total
        );
    }

    // ── Update status (admin) ─────────────────────────────────────────────────

    public async Task<ReportDto> UpdateStatusAsync(Guid reportId, UpdateReportStatusRequest request)
    {
        if (!Enum.TryParse<ReportStatus>(request.Status, ignoreCase: true, out var newStatus))
            throw new ArgumentException($"Invalid Status: '{request.Status}'.");

        var report = await _db.Reports
            .Include(r => r.Reporter)
            .FirstOrDefaultAsync(r => r.Id == reportId);

        if (report is null)
            throw new KeyNotFoundException($"Report {reportId} not found.");

        report.Status = newStatus;
        await _db.SaveChangesAsync();

        return ToDto(report, report.Reporter.DisplayName);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task VerifyTargetExistsAsync(ReportTargetType targetType, Guid targetId)
    {
        bool exists = targetType switch
        {
            ReportTargetType.Post =>
                await _db.Posts.AnyAsync(p => p.Id == targetId && p.DeletedAt == null),
            ReportTargetType.User =>
                await _db.Users.AnyAsync(u => u.Id == targetId),
            ReportTargetType.Comment =>
                await _db.Comments.AnyAsync(c => c.Id == targetId && !c.IsDeleted),
            _ => false,
        };

        if (!exists)
            throw new KeyNotFoundException($"{targetType} with id {targetId} not found.");
    }

    private static ReportDto ToDto(Report report, string reporterName) => new(
        Id: report.Id,
        ReporterId: report.ReporterId,
        ReporterName: reporterName,
        TargetType: report.TargetType.ToString(),
        TargetId: report.TargetId,
        Reason: report.Reason.ToString(),
        Notes: report.Notes,
        Status: report.Status.ToString(),
        CreatedAt: report.CreatedAt
    );
}
