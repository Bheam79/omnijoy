using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Cache hit/miss behaviour of <see cref="PostService.GetFeedAsync"/> + the
/// in/out contract of <see cref="DistributedFeedCache"/>.
/// </summary>
public class FeedCacheTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly IDistributedCache _cache;
    private readonly DistributedFeedCache _feedCache;
    private readonly PostService _sut;
    private readonly Mock<IMediaStorageService> _storageMock;

    public FeedCacheTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new OmnijoyDbContext(options);
        _storageMock = new Mock<IMediaStorageService>();

        _cache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));
        _feedCache = new DistributedFeedCache(_cache, NullLogger<DistributedFeedCache>.Instance);

        var privacy = new PrivacyService(_db);
        _sut = new PostService(_db, _storageMock.Object, privacy, _feedCache);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (_cache is IDisposable d) d.Dispose();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<User> CreateUserAsync(string displayName = "Alice", bool active = true)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@test.com",
            DisplayName = displayName,
            Gender = Gender.NotDisclosed,
            IsActive = active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<Post> AddPostAsync(
        User author,
        string content,
        PrivacyLevel privacy = PrivacyLevel.Everyone,
        DateTime? createdAt = null,
        int reactionCount = 0)
    {
        var post = new Post
        {
            Id = Guid.NewGuid(),
            AuthorUserId = author.Id,
            Author = author,
            Content = content,
            PostType = PostType.Text,
            Privacy = privacy,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Posts.Add(post);
        for (int i = 0; i < reactionCount; i++)
        {
            _db.PostReactions.Add(new PostReaction
            {
                Id = Guid.NewGuid(),
                PostId = post.Id,
                UserId = Guid.NewGuid(),
                ReactionType = ReactionType.Like,
                CreatedAt = DateTime.UtcNow,
            });
        }
        await _db.SaveChangesAsync();
        return post;
    }

    // ── Page-1 cache: miss → DB hit, then populates cache ────────────────────

    [Fact]
    public async Task GetFeedAsync_Page1_FirstCall_PopulatesCacheOnMiss()
    {
        var alice = await CreateUserAsync();
        await AddPostAsync(alice, "Post A");

        // Pre-check: cache empty.
        (await _feedCache.GetUserFeedPage1Async(alice.Id)).Should().BeNull();

        var feed = await _sut.GetFeedAsync(alice.Id, page: 1, pageSize: 20);
        feed.Items.Should().HaveCount(1);

        // After the call, the page-1 key should be populated.
        var cached = await _feedCache.GetUserFeedPage1Async(alice.Id);
        cached.Should().NotBeNull();
        cached!.Items.Should().HaveCount(1);
        cached.Items[0].Post!.Content.Should().Be("Post A");
    }

    // ── Page-1 cache: hit serves from cache without re-querying DB ───────────

    [Fact]
    public async Task GetFeedAsync_Page1_SecondCall_ServesFromCache()
    {
        var alice = await CreateUserAsync();
        await AddPostAsync(alice, "Original post");

        // First call populates the cache.
        var first = await _sut.GetFeedAsync(alice.Id, 1, 20);
        first.Items.Should().HaveCount(1);

        // Now mutate the DB directly. If the cache is being honoured, the
        // second feed read MUST still show the original snapshot.
        await AddPostAsync(alice, "Sneakier post");
        (await _db.Posts.CountAsync()).Should().Be(2);

        var second = await _sut.GetFeedAsync(alice.Id, 1, 20);
        second.Items.Should().HaveCount(1, "the cached page-1 should still be served");
        second.Items[0].Post!.Content.Should().Be("Original post");
    }

    // ── Page-1 cache: invalidation forces fresh DB read ──────────────────────

    [Fact]
    public async Task InvalidateUserFeedAsync_ForcesFreshDbReadOnNextCall()
    {
        var alice = await CreateUserAsync();
        await AddPostAsync(alice, "Original post");

        await _sut.GetFeedAsync(alice.Id, 1, 20); // populates cache

        await AddPostAsync(alice, "Brand new post");

        // Invalidate — next read should reflect both posts.
        await _feedCache.InvalidateUserFeedAsync(alice.Id);

        var after = await _sut.GetFeedAsync(alice.Id, 1, 20);
        after.Items.Should().HaveCount(2);
        after.Items.Select(i => i.Post!.Content).Should().Contain("Brand new post");
    }

    // ── Non-default page size bypasses the cache ─────────────────────────────

    [Fact]
    public async Task GetFeedAsync_CustomPageSize_DoesNotUseCache()
    {
        var alice = await CreateUserAsync();
        await AddPostAsync(alice, "Post A");

        var feed = await _sut.GetFeedAsync(alice.Id, page: 1, pageSize: 5);
        feed.Items.Should().HaveCount(1);

        // No page-1 key written (cache key only matches default page size).
        var cached = await _feedCache.GetUserFeedPage1Async(alice.Id);
        cached.Should().BeNull();
    }

    // ── Page 2+ never caches ─────────────────────────────────────────────────

    [Fact]
    public async Task GetFeedAsync_Page2_DoesNotUseCache()
    {
        var alice = await CreateUserAsync();
        for (int i = 0; i < 25; i++)
            await AddPostAsync(alice, $"P{i}", createdAt: DateTime.UtcNow.AddSeconds(i));

        var page2 = await _sut.GetFeedAsync(alice.Id, page: 2, pageSize: 20);
        page2.Items.Should().HaveCount(5);

        // Page 1 key wasn't touched by the page-2 read.
        (await _feedCache.GetUserFeedPage1Async(alice.Id)).Should().BeNull();
    }

    // ── Trending cache: round-trip ───────────────────────────────────────────

    [Fact]
    public async Task TrendingCache_RoundTrip_PreservesShape()
    {
        var dto = new PostDto(
            Id: Guid.NewGuid(),
            Author: new PostAuthorDto(Guid.NewGuid(), "Author", null),
            CompanyPageId: null,
            Content: "Cached trending",
            BackgroundImageUrl: null,
            PostType: "Text",
            Privacy: "Everyone",
            Media: Array.Empty<PostMediaItemDto>(),
            LinkPreview: null,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow);

        await _feedCache.SetTrendingPostsAsync(new[] { dto });

        var back = await _feedCache.GetTrendingPostsAsync();
        back.Should().NotBeNull();
        back!.Should().HaveCount(1);
        back[0].Content.Should().Be("Cached trending");
    }

    [Fact]
    public async Task TrendingCache_Miss_ReturnsNull()
    {
        var got = await _feedCache.GetTrendingPostsAsync();
        got.Should().BeNull();
    }

    // ── Trending query: ordering + filtering ─────────────────────────────────

    [Fact]
    public async Task GetTrendingPostsAsync_OrdersByReactionCount_PublicPostsOnly()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");

        var low  = await AddPostAsync(alice, "low",   PrivacyLevel.Everyone, reactionCount: 1);
        var high = await AddPostAsync(alice, "high",  PrivacyLevel.Everyone, reactionCount: 7);
        var mid  = await AddPostAsync(bob,   "mid",   PrivacyLevel.Everyone, reactionCount: 3);
        // Private post (Friends) must be excluded — even with high reactions.
        var hiddenPrivate = await AddPostAsync(bob, "hidden-private",
            PrivacyLevel.Friends, reactionCount: 99);
        // Stale post (>7 days) excluded.
        var hiddenStale = await AddPostAsync(alice, "hidden-stale",
            PrivacyLevel.Everyone, createdAt: DateTime.UtcNow.AddDays(-10), reactionCount: 50);

        var trending = await _sut.GetTrendingPostsAsync(take: 10);

        trending.Select(p => p.Content).Should().Equal("high", "mid", "low");
    }

    [Fact]
    public async Task GetTrendingPostsAsync_NoEligiblePosts_ReturnsEmpty()
    {
        var alice = await CreateUserAsync();
        await AddPostAsync(alice, "private only", PrivacyLevel.OnlyMe);

        var trending = await _sut.GetTrendingPostsAsync(take: 10);
        trending.Should().BeEmpty();
    }

    // ── DistributedFeedCache: key scheme ─────────────────────────────────────

    [Fact]
    public void FeedKey_FollowsDocumentedScheme()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        DistributedFeedCache.UserFeedKey(id).Should().Be("feed:11111111222233334444555555555555:p1");
        DistributedFeedCache.TrendingKey.Should().Be("trending:posts");
    }

    // ── DistributedFeedCache: invalidate-many ────────────────────────────────

    [Fact]
    public async Task InvalidateUserFeedsAsync_RemovesAllRequestedKeys()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");

        await AddPostAsync(alice, "a1");
        await AddPostAsync(bob, "b1");
        await _sut.GetFeedAsync(alice.Id, 1, 20);
        await _sut.GetFeedAsync(bob.Id, 1, 20);

        (await _feedCache.GetUserFeedPage1Async(alice.Id)).Should().NotBeNull();
        (await _feedCache.GetUserFeedPage1Async(bob.Id)).Should().NotBeNull();

        await _feedCache.InvalidateUserFeedsAsync(new[] { alice.Id, bob.Id });

        (await _feedCache.GetUserFeedPage1Async(alice.Id)).Should().BeNull();
        (await _feedCache.GetUserFeedPage1Async(bob.Id)).Should().BeNull();
    }

    // ── PostService without a cache injected: still works (null-safe) ────────

    [Fact]
    public async Task PostService_WithoutFeedCache_StillReturnsFeedFromDb()
    {
        var noCacheSvc = new PostService(_db, _storageMock.Object, new PrivacyService(_db));
        var alice = await CreateUserAsync();
        await AddPostAsync(alice, "no-cache post");

        var feed = await noCacheSvc.GetFeedAsync(alice.Id, 1, 20);
        feed.Items.Should().HaveCount(1);
    }
}
