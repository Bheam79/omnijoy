using Omnijoy.Core.Services;

namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Reason a slug-set attempt could fail.  Maps 1:1 onto the values returned
/// by <c>GET /api/slugs/check</c> so the controller can pass them straight
/// through.
/// </summary>
public enum SlugUnavailableReason
{
    /// <summary>Failed <see cref="SlugValidator"/> format rules.</summary>
    Invalid,
    /// <summary>Hit the reserved-word list.</summary>
    Reserved,
    /// <summary>Already claimed by another user or company page.</summary>
    Taken,
}

/// <summary>
/// Identifies what (if anything) a slug currently resolves to.
/// </summary>
public enum SlugOwnerType
{
    User,
    Company,
}

/// <summary>Result of a successful slug resolution.</summary>
public record SlugOwner(SlugOwnerType Type, Guid Id);

/// <summary>Result of a slug availability check.</summary>
public record SlugAvailability(bool Available, SlugUnavailableReason? Reason);

/// <summary>
/// Centralised vanity-slug logic.  Owns:
///   • validation (delegated to <see cref="SlugValidator"/>),
///   • cross-table uniqueness (Users + CompanyPages),
///   • case-insensitive resolution,
///   • set/clear operations for users and company pages, with race-safe
///     handling of unique-index violations.
/// </summary>
public interface ISlugService
{
    /// <summary>
    /// Reports whether <paramref name="slug"/> is available for any new owner,
    /// without claiming it.  Returns the first failure reason.
    /// </summary>
    Task<SlugAvailability> CheckAsync(string? slug, CancellationToken ct = default);

    /// <summary>
    /// Resolves a slug to its owning user or company page (case-insensitive,
    /// matches against the canonical lowercased form). Returns null on miss.
    /// </summary>
    Task<SlugOwner?> ResolveAsync(string slug, CancellationToken ct = default);

    /// <summary>
    /// Sets or clears the slug for <paramref name="userId"/>.
    /// Pass <c>null</c> to clear. Throws <see cref="InvalidOperationException"/>
    /// on validation / reservation / collision failures.
    /// </summary>
    Task<string?> SetUserSlugAsync(Guid userId, string? slug, CancellationToken ct = default);

    /// <summary>
    /// Sets or clears the slug for company page <paramref name="companyPageId"/>.
    /// Caller must be an Owner or Admin of the page (enforced by this service).
    /// Pass <c>null</c> to clear.
    /// </summary>
    Task<string?> SetCompanyPageSlugAsync(
        Guid companyPageId,
        Guid requesterId,
        string? slug,
        CancellationToken ct = default);
}
