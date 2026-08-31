using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Omnijoy.Core.DTOs;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

public class PostServiceBookmarkTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly Mock<IFeedCache> _cache = new();
    private readonly PostService _sut;

    public PostServiceBookmarkTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new OmnijoyDbContext(options);

        _cache.Setup(c => c.GetUserFeedPage1Async(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagedResult<FeedItemDto>?)null);
        _cache.Setup(c => c.SetUserFeedPage1Async(
                It.IsAny<Guid>(), It.IsAny<PagedResult<FeedItemDto>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var storage = Mock.Of<IMediaStorageService>();
        _sut = new PostService(_db, storage, new PrivacyService(_db), _cache.Object);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetPostAsync_HydratesTrueAndFalseBookmarkFlags()
    {
        var author = await AddUserAsync("Author");
        var viewer = await AddUserAsync("Viewer");
        var saved = await AddPostAsync(author, "saved");
        var unsaved = await AddPostAsync(author, "not saved");
        await SaveAsync(viewer.Id, saved.Id);

        (await _sut.GetPostAsync(saved.Id, viewer.Id)).IsSavedByMe.Should().BeTrue();
        (await _sut.GetPostAsync(unsaved.Id, viewer.Id)).IsSavedByMe.Should().BeFalse();
    }

    [Fact]
    public async Task GetFeedAsync_HydratesNestedSharedPost()
    {
        var viewer = await AddUserAsync("Viewer");
        var sharer = await AddUserAsync("Sharer");
        var author = await AddUserAsync("Author");
        await MakeFriendsAsync(viewer.Id, sharer.Id);
        var original = await AddPostAsync(author, "original");
        await SaveAsync(viewer.Id, original.Id);

        _db.SharedPosts.Add(new SharedPost
        {
            Id = Guid.NewGuid(),
            OriginalPostId = original.Id,
            SharerId = sharer.Id,
            TargetType = ShareTargetType.OwnWall,
            CreatedAt = DateTime.UtcNow.AddMinutes(1),
        });
        await _db.SaveChangesAsync();

        var result = await _sut.GetFeedAsync(viewer.Id, 1, 20);

        result.Items.Single(i => i.SharedPost is not null)
            .SharedPost!.OriginalPost.IsSavedByMe.Should().BeTrue();
    }

    [Fact]
    public async Task GetPostAsync_ExposesSaveCountOnlyToAuthor()
    {
        var author = await AddUserAsync("Author");
        var viewer = await AddUserAsync("Viewer");
        var otherSaver = await AddUserAsync("Other");
        var post = await AddPostAsync(author, "popular");
        await SaveAsync(viewer.Id, post.Id);
        await SaveAsync(otherSaver.Id, post.Id);

        var ownerResult = await _sut.GetPostAsync(post.Id, author.Id);
        var viewerResult = await _sut.GetPostAsync(post.Id, viewer.Id);

        ownerResult.SavesCount.Should().Be(2);
        viewerResult.SavesCount.Should().BeNull();
        JsonSerializer.Serialize(viewerResult).Should().NotContain("SavesCount");
        JsonSerializer.Serialize(viewerResult, new JsonSerializerOptions
            { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }).Should().NotContain("savesCount");
    }

    [Fact]
    public async Task GetFeedAsync_CachesNeutralDtoAndRehydratesBookmarkOnHit()
    {
        var author = await AddUserAsync("Author");
        var post = await AddPostAsync(author, "cached");
        PagedResult<FeedItemDto>? cached = null;
        _cache.Setup(c => c.GetUserFeedPage1Async(author.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => cached);
        _cache.Setup(c => c.SetUserFeedPage1Async(
                author.Id, It.IsAny<PagedResult<FeedItemDto>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, PagedResult<FeedItemDto>, CancellationToken>((_, result, _) => cached = result)
            .Returns(Task.CompletedTask);

        var first = await _sut.GetFeedAsync(author.Id, 1, 20);
        first.Items.Single().Post!.SavesCount.Should().Be(0);
        cached!.Items.Single().Post!.SavesCount.Should().BeNull("private metrics must not enter the cache");
        cached.Items.Single().Post!.IsSavedByMe.Should().BeFalse();

        await SaveAsync(author.Id, post.Id);
        var second = await _sut.GetFeedAsync(author.Id, 1, 20);

        second.Items.Single().Post!.IsSavedByMe.Should().BeTrue();
        second.Items.Single().Post!.SavesCount.Should().Be(1);
        cached.Items.Single().Post!.IsSavedByMe.Should().BeFalse("the cached snapshot stays viewer-neutral");
    }

    [Fact]
    public async Task ViewerStateHydrator_UsesTwoBatchQueriesForAnEntirePage()
    {
        var queryPlans = new List<string>();
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .EnableServiceProviderCaching(false)
            .LogTo(queryPlans.Add, [CoreEventId.QueryExecutionPlanned], LogLevel.Debug)
            .Options;
        await using var db = new OmnijoyDbContext(options);
        var requesterId = Guid.NewGuid();
        var posts = Enumerable.Range(0, 40)
            .Select(_ => (PostId: Guid.NewGuid(), AuthorId: requesterId))
            .ToArray();

        await PostViewerStateHydrator.LoadAsync(db, requesterId, posts);

        queryPlans.Should().HaveCount(2,
            "bookmark flags and author totals are each loaded once for the whole page, not once per post");
    }

    private async Task<User> AddUserAsync(string name)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@test.example",
            DisplayName = name,
            Gender = Gender.NotDisclosed,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<Post> AddPostAsync(User author, string content)
    {
        var post = new Post
        {
            Id = Guid.NewGuid(),
            AuthorUserId = author.Id,
            Content = content,
            PostType = PostType.Text,
            Privacy = PrivacyLevel.Everyone,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        return post;
    }

    private async Task SaveAsync(Guid userId, Guid postId)
    {
        _db.SavedPosts.Add(new SavedPost
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PostId = postId,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    private async Task MakeFriendsAsync(Guid first, Guid second)
    {
        _db.Friends.Add(new Friend
        {
            Id = Guid.NewGuid(),
            RequesterId = first,
            AddresseeId = second,
            Status = FriendStatus.Accepted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }
}
