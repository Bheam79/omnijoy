namespace Omnijoy.Core.Interfaces;

/// <summary>A user matched to the slug snapshot found in content.</summary>
public sealed record ResolvedMention(Guid UserId, string MatchedSlug);

/// <summary>Bulk-resolves canonical mention slugs to users with vanity URLs.</summary>
public interface IMentionResolver
{
    Task<IReadOnlyList<ResolvedMention>> ResolveUsersAsync(
        IEnumerable<string> slugs,
        CancellationToken ct = default);
}
