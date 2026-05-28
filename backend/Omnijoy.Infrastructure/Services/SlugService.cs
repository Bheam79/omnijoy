using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Core.Services;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

/// <summary>
/// EF-Core-backed <see cref="ISlugService"/>.
///
/// Race safety: the per-table unique index is the source of truth. We do a
/// pre-flight existence check for the common case, then catch
/// <see cref="DbUpdateException"/> on save and retranslate it into a
/// <see cref="SlugUnavailableReason.Taken"/> error if another concurrent
/// request claimed the slug between the check and the write.
/// </summary>
public class SlugService : ISlugService
{
    private readonly OmnijoyDbContext _db;

    public SlugService(OmnijoyDbContext db) => _db = db;

    // ── Check ─────────────────────────────────────────────────────────────────

    public async Task<SlugAvailability> CheckAsync(string? slug, CancellationToken ct = default)
    {
        var normalized = SlugValidator.Normalize(slug);
        var validation = SlugValidator.Validate(normalized);
        if (validation == SlugValidationResult.Invalid)
            return new SlugAvailability(false, SlugUnavailableReason.Invalid);
        if (validation == SlugValidationResult.Reserved)
            return new SlugAvailability(false, SlugUnavailableReason.Reserved);

        // normalized is non-null when validation passes.
        var taken = await IsSlugTakenAsync(normalized!, excludeUserId: null, excludeCompanyId: null, ct);
        if (taken) return new SlugAvailability(false, SlugUnavailableReason.Taken);

        return new SlugAvailability(true, null);
    }

    // ── Resolve ───────────────────────────────────────────────────────────────

    public async Task<SlugOwner?> ResolveAsync(string slug, CancellationToken ct = default)
    {
        var normalized = SlugValidator.Normalize(slug);
        if (normalized is null) return null;

        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.UrlSlug == normalized)
            .Select(u => new { u.Id })
            .FirstOrDefaultAsync(ct);
        if (user is not null) return new SlugOwner(SlugOwnerType.User, user.Id);

        var company = await _db.CompanyPages
            .AsNoTracking()
            .Where(c => c.UrlSlug == normalized)
            .Select(c => new { c.Id })
            .FirstOrDefaultAsync(ct);
        if (company is not null) return new SlugOwner(SlugOwnerType.Company, company.Id);

        return null;
    }

    // ── Set user slug ─────────────────────────────────────────────────────────

    public async Task<string?> SetUserSlugAsync(Guid userId, string? slug, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        if (slug is null || string.IsNullOrWhiteSpace(slug))
        {
            user.UrlSlug = null;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return null;
        }

        var normalized = await ValidateAndCheckAvailabilityAsync(slug, excludeUserId: userId, excludeCompanyId: null, ct);

        user.UrlSlug = normalized;
        user.UpdatedAt = DateTime.UtcNow;
        await SaveOrTranslateUniqueViolationAsync(ct);
        return normalized;
    }

    // ── Set company page slug ─────────────────────────────────────────────────

    public async Task<string?> SetCompanyPageSlugAsync(
        Guid companyPageId,
        Guid requesterId,
        string? slug,
        CancellationToken ct = default)
    {
        var page = await _db.CompanyPages
            .Include(c => c.Admins)
            .FirstOrDefaultAsync(c => c.Id == companyPageId, ct)
            ?? throw new KeyNotFoundException($"Company page {companyPageId} not found.");

        // Owner or Admin only — Editors cannot change the public URL.
        var role = page.Admins.FirstOrDefault(a => a.UserId == requesterId)?.Role;
        if (role is null || role == CompanyPageRole.Editor)
            throw new UnauthorizedAccessException("Only Owners and Admins can set the page's URL slug.");

        if (slug is null || string.IsNullOrWhiteSpace(slug))
        {
            page.UrlSlug = null;
            await _db.SaveChangesAsync(ct);
            return null;
        }

        var normalized = await ValidateAndCheckAvailabilityAsync(slug, excludeUserId: null, excludeCompanyId: companyPageId, ct);

        page.UrlSlug = normalized;
        await SaveOrTranslateUniqueViolationAsync(ct);
        return normalized;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> ValidateAndCheckAvailabilityAsync(
        string slug,
        Guid? excludeUserId,
        Guid? excludeCompanyId,
        CancellationToken ct)
    {
        var normalized = SlugValidator.Normalize(slug);
        var validation = SlugValidator.Validate(normalized);
        switch (validation)
        {
            case SlugValidationResult.Invalid:
                throw new InvalidOperationException("invalid");
            case SlugValidationResult.Reserved:
                throw new InvalidOperationException("reserved");
        }

        var taken = await IsSlugTakenAsync(normalized!, excludeUserId, excludeCompanyId, ct);
        if (taken) throw new InvalidOperationException("taken");

        return normalized!;
    }

    /// <summary>
    /// Returns true if <paramref name="normalized"/> is already claimed by any
    /// user or company page, excluding the current owner (so re-saving the
    /// same slug for the same owner doesn't false-alarm as a collision).
    /// </summary>
    private async Task<bool> IsSlugTakenAsync(
        string normalized,
        Guid? excludeUserId,
        Guid? excludeCompanyId,
        CancellationToken ct)
    {
        var userMatch = await _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.UrlSlug == normalized && (excludeUserId == null || u.Id != excludeUserId), ct);
        if (userMatch) return true;

        var companyMatch = await _db.CompanyPages
            .AsNoTracking()
            .AnyAsync(c => c.UrlSlug == normalized && (excludeCompanyId == null || c.Id != excludeCompanyId), ct);
        return companyMatch;
    }

    /// <summary>
    /// Wraps SaveChangesAsync so a concurrent unique-index violation surfaces
    /// as the same "taken" error callers see from the pre-flight check.
    /// </summary>
    private async Task SaveOrTranslateUniqueViolationAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new InvalidOperationException("taken", ex);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        // Pomelo MySql connector / EF surfaces unique-index violations as
        // MySqlException with Number 1062. We string-sniff to avoid taking
        // a hard dependency on the connector type in Core/Infrastructure.
        var message = ex.GetBaseException().Message;
        return message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            || message.Contains("1062", StringComparison.OrdinalIgnoreCase);
    }
}
