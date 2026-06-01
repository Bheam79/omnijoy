using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;
using System.IO;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Extended unit tests for <see cref="PostService"/> covering paths not
/// exercised in <see cref="PostServiceTests"/>:
///   • GetTrendingPostsAsync
///   • GetFeedAsync — cache hit/miss, blocked users, inactive authors, shared posts
///   • CreatePostAsync — company-page validation, video media, thumbnail enqueueing
///   • UpdatePostAsync — privacy update
///   • GetPostAsync — blocked-user and FriendsOfFriends access
/// </summary>
public class PostServiceExtendedTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly Mock<IMediaStorageService> _storageMock;
    private readonly Mock<IImageProcessingService> _imageProcessorMock;
    private readonly Mock<IFeedCache> _feedCacheMock;
    private readonly Mock<IThumbnailService> _thumbnailMock;
    private readonly PostService _sut;

    public PostServiceExtendedTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new OmnijoyDbContext(options);
        _storageMock = new Mock<IMediaStorageService>();
        _imageProcessorMock = new Mock<IImageProcessingService>();
        _feedCacheMock = new Mock<IFeedCache>();
        _thumbnailMock = new Mock<IThumbnailService>();

        _imageProcessorMock
            .Setup(p => p.ProcessImageAsync(It.IsAny<Stream>(), It.IsAny<ImageFolder>()))
            .ReturnsAsync((Stream s, ImageFolder _) => s);

        _storageMock
            .Setup(s => s.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("/uploads/test/file.jpg");

        _thumbnailMock
            .Setup(t => t.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Default: no cached feed (cache miss → go to DB)
        _feedCacheMock
            .Setup(c => c.GetUserFeedPage1Async(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FeedPageResult?)null);

        _feedCacheMock
            .Setup(c => c.SetUserFeedPage1Async(It.IsAny<Guid>(), It.IsAny<FeedPageResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var privacy = new PrivacyService(_db);
        _sut = new PostService(_db, _storageMock.Object, privacy, _feedCacheMock.Object, _imageProcessorMock.Object, _thumbnailMock.Object);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<User> CreateUserAsync(string name = "User", bool isActive = true)
    {
        var user = new User
        {
            Id          = Guid.NewGuid(),
            Email       = $"{Guid.NewGuid()}@test.com",
            DisplayName = name,
            Gender      = Gender.NotDisclosed,
            IsActive    = isActive,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task MakeFriendsAsync(Guid a, Guid b)
    {
        _db.Friends.Add(new Friend
        {
            Id          = Guid.NewGuid(),
            RequesterId = a,
            AddresseeId = b,
            Status      = FriendStatus.Accepted,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    private async Task BlockAsync(Guid blocker, Guid blocked)
    {
        _db.Friends.Add(new Friend
        {
            Id          = Guid.NewGuid(),
            RequesterId = blocker,
            AddresseeId = blocked,
            Status      = FriendStatus.Blocked,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    private async Task<Post> CreatePublicPostAsync(User author, string content = "Public post", int daysOld = 0)
    {
        var post = new Post
        {
            Id           = Guid.NewGuid(),
            AuthorUserId = author.Id,
            Author       = author,
            Content      = content,
            PostType     = PostType.Text,
            Privacy      = PrivacyLevel.Everyone,
            CreatedAt    = DateTime.UtcNow.AddDays(-daysOld),
            UpdatedAt    = DateTime.UtcNow,
        };
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        return post;
    }

    private async Task AddReactionsAsync(Guid postId, int count)
    {
        for (int i = 0; i < count; i++)
        {
            _db.PostReactions.Add(new PostReaction
            {
                Id           = Guid.NewGuid(),
                PostId       = postId,
                UserId       = Guid.NewGuid(),
                ReactionType = ReactionType.Like,
                CreatedAt    = DateTime.UtcNow,
            });
        }
        await _db.SaveChangesAsync();
    }

    // ── GetTrendingPostsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetTrending_PublicRecentPosts_ReturnsSortedByReactions()
    {
        var alice = await CreateUserAsync("Alice");
        var popular = await CreatePublicPostAsync(alice, "Popular");
        var quiet   = await CreatePublicPostAsync(alice, "Quiet");

        await AddReactionsAsync(popular.Id, 10);
        await AddReactionsAsync(quiet.Id, 1);

        var trending = await _sut.GetTrendingPostsAsync(20);

        trending.Should().HaveCount(2);
        trending[0].Id.Should().Be(popular.Id);
        trending[1].Id.Should().Be(quiet.Id);
    }

    [Fact]
    public async Task GetTrending_PrivatePosts_NotIncluded()
    {
        var alice = await CreateUserAsync("Alice");
        _db.Posts.Add(new Post
        {
            Id           = Guid.NewGuid(),
            AuthorUserId = alice.Id,
            Author       = alice,
            Content      = "Private",
            PostType     = PostType.Text,
            Privacy      = PrivacyLevel.OnlyMe,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var trending = await _sut.GetTrendingPostsAsync(20);

        trending.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTrending_PostsOlderThanSevenDays_NotIncluded()
    {
        var alice = await CreateUserAsync("Alice");
        await CreatePublicPostAsync(alice, "Old post", daysOld: 8);

        var trending = await _sut.GetTrendingPostsAsync(20);

        trending.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTrending_PostsFromInactiveAuthors_NotIncluded()
    {
        var deactivated = await CreateUserAsync("Deactivated", isActive: false);
        await CreatePublicPostAsync(deactivated, "From inactive user");

        var trending = await _sut.GetTrendingPostsAsync(20);

        trending.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTrending_NoPosts_ReturnsEmpty()
    {
        var trending = await _sut.GetTrendingPostsAsync(20);

        trending.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTrending_TakeLimitRespected()
    {
        var alice = await CreateUserAsync("Alice");
        for (int i = 0; i < 10; i++)
            await CreatePublicPostAsync(alice, $"Post {i}");

        var trending = await _sut.GetTrendingPostsAsync(take: 3);

        trending.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetTrending_TakeZero_ClampedToOne()
    {
        var alice = await CreateUserAsync("Alice");
        await CreatePublicPostAsync(alice, "Post");

        var trending = await _sut.GetTrendingPostsAsync(take: 0);

        trending.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTrending_TakeOverHundred_ClampedTo100()
    {
        var alice = await CreateUserAsync("Alice");
        for (int i = 0; i < 5; i++)
            await CreatePublicPostAsync(alice, $"Post {i}");

        // take=200 should be clamped to 100; we only have 5 posts so we get 5
        var trending = await _sut.GetTrendingPostsAsync(take: 200);

        trending.Should().HaveCount(5);
    }

    // ── GetFeedAsync — cache ──────────────────────────────────────────────────

    [Fact]
    public async Task GetFeed_CacheHit_ReturnsCachedResult()
    {
        var userId = Guid.NewGuid();
        var cachedResult = new FeedPageResult(
            Items: [],
            Page: 1,
            PageSize: 20,
            HasMore: false);

        _feedCacheMock
            .Setup(c => c.GetUserFeedPage1Async(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResult);

        var result = await _sut.GetFeedAsync(userId, 1, 20);

        result.Should().BeSameAs(cachedResult);
    }

    [Fact]
    public async Task GetFeed_CacheMiss_WritesResultToCache()
    {
        var user = await CreateUserAsync();

        var result = await _sut.GetFeedAsync(user.Id, 1, 20);

        _feedCacheMock.Verify(
            c => c.SetUserFeedPage1Async(user.Id, It.IsAny<FeedPageResult>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetFeed_PageTwo_DoesNotCheckCache()
    {
        var user = await CreateUserAsync();

        await _sut.GetFeedAsync(user.Id, 2, 20);

        _feedCacheMock.Verify(
            c => c.GetUserFeedPage1Async(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetFeed_CustomPageSize_DoesNotCheckCache()
    {
        var user = await CreateUserAsync();

        await _sut.GetFeedAsync(user.Id, 1, 10); // non-default size

        _feedCacheMock.Verify(
            c => c.GetUserFeedPage1Async(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── GetFeedAsync — blocking ───────────────────────────────────────────────

    [Fact]
    public async Task GetFeed_BlockedUser_TheirPostsNotInFeed()
    {
        var alice   = await CreateUserAsync("Alice");
        var blocked = await CreateUserAsync("Blocked");
        await BlockAsync(alice.Id, blocked.Id);

        _db.Posts.Add(new Post
        {
            Id           = Guid.NewGuid(),
            AuthorUserId = blocked.Id,
            Author       = blocked,
            Content      = "Blocked user post",
            PostType     = PostType.Text,
            Privacy      = PrivacyLevel.Everyone,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var feed = await _sut.GetFeedAsync(alice.Id, 1, 20);

        feed.Items.Should().NotContain(i => i.Post != null && i.Post.Content == "Blocked user post");
    }

    // ── GetFeedAsync — inactive authors ──────────────────────────────────────

    [Fact]
    public async Task GetFeed_InactiveAuthor_TheirPostsNotInFeed()
    {
        var viewer      = await CreateUserAsync("Viewer");
        var deactivated = await CreateUserAsync("Deactivated", isActive: false);

        _db.Posts.Add(new Post
        {
            Id           = Guid.NewGuid(),
            AuthorUserId = deactivated.Id,
            Author       = deactivated,
            Content      = "From deactivated",
            PostType     = PostType.Text,
            Privacy      = PrivacyLevel.Everyone,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var feed = await _sut.GetFeedAsync(viewer.Id, 1, 20);

        feed.Items.Should().NotContain(i => i.Post != null && i.Post.Content == "From deactivated");
    }

    // ── GetFeedAsync — shared posts ───────────────────────────────────────────

    [Fact]
    public async Task GetFeed_FriendSharesPostOnOwnWall_AppearsInFeed()
    {
        var alice = await CreateUserAsync("Alice");
        var bob   = await CreateUserAsync("Bob");
        await MakeFriendsAsync(alice.Id, bob.Id);

        var original = await CreatePublicPostAsync(alice, "Original");

        _db.SharedPosts.Add(new SharedPost
        {
            Id             = Guid.NewGuid(),
            OriginalPostId = original.Id,
            OriginalPost   = original,
            SharerId       = bob.Id,
            Sharer         = bob,
            TargetType     = ShareTargetType.OwnWall,
            CreatedAt      = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var feed = await _sut.GetFeedAsync(alice.Id, 1, 20);

        feed.Items.Should().Contain(i => i.ItemType == "SharedPost");
    }

    [Fact]
    public async Task GetFeed_FriendSharesPostOnMyWall_AppearsInFeed()
    {
        var alice = await CreateUserAsync("Alice");
        var bob   = await CreateUserAsync("Bob");
        await MakeFriendsAsync(alice.Id, bob.Id);

        var original = await CreatePublicPostAsync(alice, "Original");

        _db.SharedPosts.Add(new SharedPost
        {
            Id             = Guid.NewGuid(),
            OriginalPostId = original.Id,
            OriginalPost   = original,
            SharerId       = bob.Id,
            Sharer         = bob,
            TargetType     = ShareTargetType.FriendWall,
            TargetId       = alice.Id,
            CreatedAt      = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var feed = await _sut.GetFeedAsync(alice.Id, 1, 20);

        feed.Items.Should().Contain(i => i.ItemType == "SharedPost");
    }

    [Fact]
    public async Task GetFeed_StrangerSharesPost_DoesNotAppearInFeed()
    {
        var alice    = await CreateUserAsync("Alice");
        var stranger = await CreateUserAsync("Stranger");
        var thirdParty = await CreateUserAsync("Third");

        var original = await CreatePublicPostAsync(thirdParty, "Third party post");

        _db.SharedPosts.Add(new SharedPost
        {
            Id             = Guid.NewGuid(),
            OriginalPostId = original.Id,
            OriginalPost   = original,
            SharerId       = stranger.Id,
            Sharer         = stranger,
            TargetType     = ShareTargetType.OwnWall,
            CreatedAt      = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var feed = await _sut.GetFeedAsync(alice.Id, 1, 20);

        feed.Items.Should().NotContain(i => i.ItemType == "SharedPost");
    }

    // ── GetFeedAsync — FriendsOfFriends privacy ───────────────────────────────

    [Fact]
    public async Task GetFeed_FriendsOnlyPost_VisibleToFriend()
    {
        var alice = await CreateUserAsync("Alice");
        var bob   = await CreateUserAsync("Bob");
        await MakeFriendsAsync(alice.Id, bob.Id);

        _db.Posts.Add(new Post
        {
            Id           = Guid.NewGuid(),
            AuthorUserId = bob.Id,
            Author       = bob,
            Content      = "Friends only post",
            PostType     = PostType.Text,
            Privacy      = PrivacyLevel.Friends,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var feed = await _sut.GetFeedAsync(alice.Id, 1, 20);

        feed.Items.Should().Contain(i => i.Post != null && i.Post.Content == "Friends only post");
    }

    // ── GetFeedAsync — pagination clamping ────────────────────────────────────

    [Fact]
    public async Task GetFeed_NegativePage_ClampedToOne()
    {
        var user = await CreateUserAsync();

        // Should not throw
        var feed = await _sut.GetFeedAsync(user.Id, -5, 20);

        feed.Page.Should().Be(1);
    }

    [Fact]
    public async Task GetFeed_PageSizeTooLarge_ClampedToTwenty()
    {
        var user = await CreateUserAsync();

        var feed = await _sut.GetFeedAsync(user.Id, 1, 200);

        feed.PageSize.Should().Be(20);
    }

    // ── GetPostAsync — access control ─────────────────────────────────────────

    [Fact]
    public async Task GetPost_BlockedByAuthor_ThrowsUnauthorized()
    {
        var alice   = await CreateUserAsync("Alice");
        var blocked = await CreateUserAsync("Blocked");
        await BlockAsync(alice.Id, blocked.Id); // alice blocks the viewer

        var postId = Guid.NewGuid();
        _db.Posts.Add(new Post
        {
            Id           = postId,
            AuthorUserId = alice.Id,
            Author       = alice,
            Content      = "Alice's post",
            PostType     = PostType.Text,
            Privacy      = PrivacyLevel.Everyone,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        await _sut.Invoking(s => s.GetPostAsync(postId, blocked.Id))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetPost_FriendsPost_VisibleToFriend()
    {
        var alice = await CreateUserAsync("Alice");
        var bob   = await CreateUserAsync("Bob");
        await MakeFriendsAsync(alice.Id, bob.Id);

        var postId = Guid.NewGuid();
        _db.Posts.Add(new Post
        {
            Id           = postId,
            AuthorUserId = alice.Id,
            Author       = alice,
            Content      = "Friends post",
            PostType     = PostType.Text,
            Privacy      = PrivacyLevel.Friends,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var dto = await _sut.GetPostAsync(postId, bob.Id);

        dto.Content.Should().Be("Friends post");
    }

    [Fact]
    public async Task GetPost_FriendsPost_NotVisibleToStranger()
    {
        var alice    = await CreateUserAsync("Alice");
        var stranger = await CreateUserAsync("Stranger");

        var postId = Guid.NewGuid();
        _db.Posts.Add(new Post
        {
            Id           = postId,
            AuthorUserId = alice.Id,
            Author       = alice,
            Content      = "Friends post",
            PostType     = PostType.Text,
            Privacy      = PrivacyLevel.Friends,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        await _sut.Invoking(s => s.GetPostAsync(postId, stranger.Id))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetPost_OnlyMePost_NotVisibleToAnyone()
    {
        var alice    = await CreateUserAsync("Alice");
        var stranger = await CreateUserAsync("Stranger");

        var postId = Guid.NewGuid();
        _db.Posts.Add(new Post
        {
            Id           = postId,
            AuthorUserId = alice.Id,
            Author       = alice,
            Content      = "Private",
            PostType     = PostType.Text,
            Privacy      = PrivacyLevel.OnlyMe,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        await _sut.Invoking(s => s.GetPostAsync(postId, stranger.Id))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── CreatePostAsync — company page ────────────────────────────────────────

    [Fact]
    public async Task CreatePost_CompanyPageAdmin_Succeeds()
    {
        var user = await CreateUserAsync("Alice");
        var page = new CompanyPage
        {
            Id               = Guid.NewGuid(),
            Name             = "Acme Inc",
            CreatedByUserId  = user.Id,
            CreatedByUser    = user,
            CreatedAt        = DateTime.UtcNow,
        };
        _db.CompanyPages.Add(page);
        _db.CompanyPageAdmins.Add(new CompanyPageAdmin
        {
            CompanyPageId = page.Id,
            UserId        = user.Id,
        });
        await _db.SaveChangesAsync();

        var dto = await _sut.CreatePostAsync(
            user.Id,
            new CreatePostRequest("Company update", "Text", "Everyone", null, page.Id),
            null);

        dto.CompanyPageId.Should().Be(page.Id);
    }

    [Fact]
    public async Task CreatePost_NotCompanyPageAdmin_ThrowsUnauthorized()
    {
        var user = await CreateUserAsync("Alice");
        var page = new CompanyPage
        {
            Id               = Guid.NewGuid(),
            Name             = "Acme Inc",
            CreatedByUserId  = user.Id,
            CreatedByUser    = user,
            CreatedAt        = DateTime.UtcNow,
        };
        _db.CompanyPages.Add(page);
        await _db.SaveChangesAsync();

        await _sut.Invoking(s => s.CreatePostAsync(
                user.Id,
                new CreatePostRequest("Hack", "Text", "Everyone", null, page.Id),
                null))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*admin*");
    }

    // ── CreatePostAsync — video media + thumbnail ─────────────────────────────

    [Fact]
    public async Task CreatePost_VideoMedia_EnqueuesThumbnailJob()
    {
        var user = await CreateUserAsync();
        _storageMock
            .Setup(s => s.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), "posts/videos"))
            .ReturnsAsync("/uploads/posts/videos/clip.mp4");

        var mediaItem = new MediaUploadItem(
            new MemoryStream(new byte[] { 0x00, 0x00 }),
            "clip.mp4",
            "video/mp4"
        );
        var request = new CreatePostRequest("Check this out", "Video", "Everyone", null);

        var dto = await _sut.CreatePostAsync(user.Id, request, [mediaItem]);

        dto.Media.Should().HaveCount(1);
        dto.Media[0].MediaType.Should().Be("Video");

        _thumbnailMock.Verify(t => t.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CreatePost_ImageMedia_ImageProcessorCalledAndNoThumbnailJob()
    {
        var user = await CreateUserAsync();
        _storageMock
            .Setup(s => s.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), "posts/images"))
            .ReturnsAsync("/uploads/posts/images/photo.webp");

        var mediaItem = new MediaUploadItem(
            new MemoryStream(new byte[] { 0xFF, 0xD8 }),
            "photo.jpg",
            "image/jpeg"
        );
        var request = new CreatePostRequest("Photo post", "Image", "Everyone", null);

        var dto = await _sut.CreatePostAsync(user.Id, request, [mediaItem]);

        dto.Media[0].MediaType.Should().Be("Image");
        _imageProcessorMock.Verify(
            p => p.ProcessImageAsync(It.IsAny<Stream>(), ImageFolder.PostImage), Times.Once);
        _thumbnailMock.Verify(
            t => t.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    // ── CreatePostAsync — link preview ────────────────────────────────────────

    [Fact]
    public async Task CreatePost_WithLinkPreview_PreservesLinkFields()
    {
        var user = await CreateUserAsync();
        var request = new CreatePostRequest(
            Content: "Check this out",
            PostType: "Text",
            Privacy: "Everyone",
            BackgroundImageUrl: null,
            CompanyPageId: null,
            LinkUrl: "https://example.com",
            LinkTitle: "Example",
            LinkDescription: "An example site",
            LinkImageUrl: "https://example.com/og.png",
            LinkSiteName: "example.com"
        );

        var dto = await _sut.CreatePostAsync(user.Id, request, null);

        dto.LinkPreview.Should().NotBeNull();
        dto.LinkPreview!.Url.Should().Be("https://example.com");
        dto.LinkPreview.Title.Should().Be("Example");
        dto.LinkPreview.SiteName.Should().Be("example.com");
    }

    // ── UpdatePostAsync — privacy change ──────────────────────────────────────

    [Fact]
    public async Task UpdatePost_PrivacyChange_PersistsCorrectly()
    {
        var user   = await CreateUserAsync();
        var postId = Guid.NewGuid();
        _db.Posts.Add(new Post
        {
            Id           = postId,
            AuthorUserId = user.Id,
            Author       = user,
            Content      = "Original",
            PostType     = PostType.Text,
            Privacy      = PrivacyLevel.Friends,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var dto = await _sut.UpdatePostAsync(postId, user.Id, new UpdatePostRequest(null, "Everyone"));

        dto.Privacy.Should().Be("Everyone");
    }

    [Fact]
    public async Task UpdatePost_InvalidPrivacy_ThrowsArgumentException()
    {
        var user   = await CreateUserAsync();
        var postId = Guid.NewGuid();
        _db.Posts.Add(new Post
        {
            Id           = postId,
            AuthorUserId = user.Id,
            Author       = user,
            Content      = "Original",
            PostType     = PostType.Text,
            Privacy      = PrivacyLevel.Friends,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        await _sut.Invoking(s => s.UpdatePostAsync(postId, user.Id, new UpdatePostRequest(null, "BadValue")))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Privacy*");
    }

    [Fact]
    public async Task UpdatePost_NotFound_ThrowsKeyNotFoundException()
    {
        var user = await CreateUserAsync();

        await _sut.Invoking(s => s.UpdatePostAsync(Guid.NewGuid(), user.Id, new UpdatePostRequest("new", null)))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── DeletePostAsync — edge case ───────────────────────────────────────────

    [Fact]
    public async Task DeletePost_NotFound_ThrowsKeyNotFoundException()
    {
        var user = await CreateUserAsync();

        await _sut.Invoking(s => s.DeletePostAsync(Guid.NewGuid(), user.Id))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Followers privacy — company posts ─────────────────────────────────────

    private async Task<CompanyPage> CreateCompanyPageAsync(Guid ownerId)
    {
        var user = await _db.Users.FindAsync(ownerId) ?? throw new InvalidOperationException();
        var page = new CompanyPage
        {
            Id              = Guid.NewGuid(),
            Name            = "Acme Corp",
            CreatedByUserId = ownerId,
            CreatedByUser   = user,
            CreatedAt       = DateTime.UtcNow,
        };
        _db.CompanyPages.Add(page);
        _db.CompanyPageAdmins.Add(new CompanyPageAdmin
        {
            CompanyPageId = page.Id,
            UserId        = ownerId,
        });
        await _db.SaveChangesAsync();
        return page;
    }

    private async Task FollowCompanyAsync(Guid pageId, Guid userId)
    {
        _db.CompanyPageFollows.Add(new CompanyPageFollow
        {
            CompanyPageId = pageId,
            UserId        = userId,
            CreatedAt     = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task CreatePost_FollowersPrivacy_WithoutCompanyPage_ThrowsArgumentException()
    {
        var user = await CreateUserAsync();

        await _sut.Invoking(s => s.CreatePostAsync(
                user.Id,
                new CreatePostRequest("Test", "Text", "Followers", null),
                null))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Followers*company*");
    }

    [Fact]
    public async Task CreatePost_FollowersPrivacy_WithCompanyPage_Succeeds()
    {
        var admin = await CreateUserAsync("Admin");
        var page  = await CreateCompanyPageAsync(admin.Id);

        var dto = await _sut.CreatePostAsync(
            admin.Id,
            new CreatePostRequest("Followers update", "Text", "Followers", null, page.Id),
            null);

        dto.Privacy.Should().Be("Followers");
        dto.CompanyPageId.Should().Be(page.Id);
    }

    [Fact]
    public async Task GetFeed_CompanyPostFollowersPrivacy_VisibleToFollower()
    {
        var admin     = await CreateUserAsync("Admin");
        var follower  = await CreateUserAsync("Follower");
        var page      = await CreateCompanyPageAsync(admin.Id);
        await FollowCompanyAsync(page.Id, follower.Id);

        _db.Posts.Add(new Post
        {
            Id            = Guid.NewGuid(),
            AuthorUserId  = admin.Id,
            Author        = admin,
            CompanyPageId = page.Id,
            CompanyPage   = page,
            Content       = "Followers-only company post",
            PostType      = PostType.Text,
            Privacy       = PrivacyLevel.Followers,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var feed = await _sut.GetFeedAsync(follower.Id, 1, 20);

        feed.Items.Should().Contain(i => i.Post != null && i.Post.Content == "Followers-only company post");
    }

    [Fact]
    public async Task GetFeed_CompanyPostFollowersPrivacy_NotVisibleToNonFollower()
    {
        var admin      = await CreateUserAsync("Admin");
        var stranger   = await CreateUserAsync("Stranger");
        var page       = await CreateCompanyPageAsync(admin.Id);

        _db.Posts.Add(new Post
        {
            Id            = Guid.NewGuid(),
            AuthorUserId  = admin.Id,
            Author        = admin,
            CompanyPageId = page.Id,
            CompanyPage   = page,
            Content       = "Followers-only company post",
            PostType      = PostType.Text,
            Privacy       = PrivacyLevel.Followers,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var feed = await _sut.GetFeedAsync(stranger.Id, 1, 20);

        feed.Items.Should().NotContain(i => i.Post != null && i.Post.Content == "Followers-only company post");
    }

    [Fact]
    public async Task GetPost_CompanyPostFollowersPrivacy_VisibleToFollower()
    {
        var admin    = await CreateUserAsync("Admin");
        var follower = await CreateUserAsync("Follower");
        var page     = await CreateCompanyPageAsync(admin.Id);
        await FollowCompanyAsync(page.Id, follower.Id);

        var postId = Guid.NewGuid();
        _db.Posts.Add(new Post
        {
            Id            = postId,
            AuthorUserId  = admin.Id,
            Author        = admin,
            CompanyPageId = page.Id,
            CompanyPage   = page,
            Content       = "Followers only",
            PostType      = PostType.Text,
            Privacy       = PrivacyLevel.Followers,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var dto = await _sut.GetPostAsync(postId, follower.Id);

        dto.Content.Should().Be("Followers only");
    }

    [Fact]
    public async Task GetPost_CompanyPostFollowersPrivacy_VisibleToAdmin()
    {
        var admin = await CreateUserAsync("Admin");
        var page  = await CreateCompanyPageAsync(admin.Id);

        var postId = Guid.NewGuid();
        _db.Posts.Add(new Post
        {
            Id            = postId,
            AuthorUserId  = admin.Id,
            Author        = admin,
            CompanyPageId = page.Id,
            CompanyPage   = page,
            Content       = "Followers only",
            PostType      = PostType.Text,
            Privacy       = PrivacyLevel.Followers,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        // The admin is also the author so EnforceReadAccess returns early — confirm it works
        var dto = await _sut.GetPostAsync(postId, admin.Id);

        dto.Content.Should().Be("Followers only");
    }

    [Fact]
    public async Task GetPost_CompanyPostFollowersPrivacy_NotVisibleToStranger()
    {
        var admin    = await CreateUserAsync("Admin");
        var stranger = await CreateUserAsync("Stranger");
        var page     = await CreateCompanyPageAsync(admin.Id);

        var postId = Guid.NewGuid();
        _db.Posts.Add(new Post
        {
            Id            = postId,
            AuthorUserId  = admin.Id,
            Author        = admin,
            CompanyPageId = page.Id,
            CompanyPage   = page,
            Content       = "Followers only",
            PostType      = PostType.Text,
            Privacy       = PrivacyLevel.Followers,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        await _sut.Invoking(s => s.GetPostAsync(postId, stranger.Id))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
