using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Services;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

/// <summary>Resolves every requested mention with one projected user query.</summary>
public sealed class MentionResolver : IMentionResolver
{
    private readonly OmnijoyDbContext _db;

    public MentionResolver(OmnijoyDbContext db) => _db = db;

    public async Task<IReadOnlyList<ResolvedMention>> ResolveUsersAsync(
        IEnumerable<string> slugs,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(slugs);

        var normalizedSlugs = slugs
            .Select(SlugValidator.Normalize)
            .Where(slug => SlugValidator.Validate(slug) == SlugValidationResult.Valid)
            .Select(slug => slug!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedSlugs.Length == 0)
            return Array.Empty<ResolvedMention>();

        var users = await _db.Users
            .AsNoTracking()
            .Where(user => user.UrlSlug != null && normalizedSlugs.Contains(user.UrlSlug))
            .Select(user => new { user.Id, user.UrlSlug })
            .ToListAsync(ct);

        return users
            .Select(user => new ResolvedMention(user.Id, SlugValidator.Normalize(user.UrlSlug)!))
            .ToArray();
    }
}
