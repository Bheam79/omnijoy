using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

public class PostServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly Mock<IMediaStorageService> _storageMock;
    private readonly PostService _sut;

    public PostServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new OmnijoyDbContext(options);
        _storageMock = new Mock<IMediaStorageService>();
        var privacy = new PrivacyService(_db);
        _sut = new PostService(_db, _storageMock.Object, privacy);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<User> CreateUserAsync(string displayName = "Alice")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@test.com",
            DisplayName = displayName,
            Gender = Gender.NotDisclosed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task MakeFriendsAsync(Guid a, Guid b)
    {
        _db.Friends.Add(new Friend
        {
            Id = Guid.NewGuid(),
            RequesterId = a,
            AddresseeId = b,
            Status = FriendStatus.Accepted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePost_TextPost_ReturnsDto()
    {
        var user = await CreateUserAsync();
        var request = new CreatePostRequest("Hello world", "Text", "Friends", null);

        var dto = await _sut.CreatePostAsync(user.Id, request, null);

        dto.Should().NotBeNull();
        dto.Content.Should().Be("Hello world");
        dto.PostType.Should().Be("Text");
        dto.Privacy.Should().Be("Friends");
        dto.Author.Id.Should().Be(user.Id);
        dto.Media.Should().BeEmpty();
    }

    [Fact]
    public async Task CreatePost_WithMedia_StoresMediaAndReturnsDto()
    {
        var user = await CreateUserAsync();
        _storageMock
            .Setup(s => s.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("/uploads/posts/images/test.jpg");

        var mediaItem = new MediaUploadItem(
            new MemoryStream(new byte[] { 0xFF, 0xD8 }),
            "photo.jpg",
            "image/jpeg"
        );
        var request = new CreatePostRequest("Look at this", "Image", "Everyone", null);

        var dto = await _sut.CreatePostAsync(user.Id, request, [mediaItem]);

        dto.Media.Should().HaveCount(1);
        dto.Media[0].MediaType.Should().Be("Image");
        dto.Media[0].Url.Should().Be("/uploads/posts/images/test.jpg");
        _storageMock.Verify(s => s.StoreAsync(It.IsAny<Stream>(), "photo.jpg", "posts/images"), Times.Once);
    }

    [Fact]
    public async Task CreatePost_InvalidPostType_ThrowsArgumentException()
    {
        var user = await CreateUserAsync();
        var request = new CreatePostRequest("Test", "SuperWeirdType", "Friends", null);

        await _sut.Invoking(s => s.CreatePostAsync(user.Id, request, null))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*PostType*");
    }

    [Fact]
    public async Task CreatePost_InvalidPrivacy_ThrowsArgumentException()
    {
        var user = await CreateUserAsync();
        var request = new CreatePostRequest("Test", "Text", "Invalid", null);

        await _sut.Invoking(s => s.CreatePostAsync(user.Id, request, null))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Privacy*");
    }

    // ── Feed ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFeed_ReturnsOwnPosts()
    {
        var user = await CreateUserAsync();
        _db.Posts.Add(new Post
        {
            Id = Guid.NewGuid(),
            AuthorUserId = user.Id,
            Author = user,
            Content = "My post",
            PostType = PostType.Text,
            Privacy = PrivacyLevel.OnlyMe, // own posts always visible
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var feed = await _sut.GetFeedAsync(user.Id, 1, 20);

        feed.Items.Should().HaveCount(1);
        feed.Items[0].Content.Should().Be("My post");
    }

    [Fact]
    public async Task GetFeed_ReturnsFriendPublicPosts()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        await MakeFriendsAsync(alice.Id, bob.Id);

        _db.Posts.Add(new Post
        {
            Id = Guid.NewGuid(),
            AuthorUserId = bob.Id,
            Author = bob,
            Content = "Bob's public post",
            PostType = PostType.Text,
            Privacy = PrivacyLevel.Everyone,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var feed = await _sut.GetFeedAsync(alice.Id, 1, 20);

        feed.Items.Should().Contain(p => p.Content == "Bob's public post");
    }

    [Fact]
    public async Task GetFeed_DoesNotReturnFriendOnlyMePosts()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        await MakeFriendsAsync(alice.Id, bob.Id);

        _db.Posts.Add(new Post
        {
            Id = Guid.NewGuid(),
            AuthorUserId = bob.Id,
            Author = bob,
            Content = "Bob's private post",
            PostType = PostType.Text,
            Privacy = PrivacyLevel.OnlyMe,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var feed = await _sut.GetFeedAsync(alice.Id, 1, 20);

        feed.Items.Should().NotContain(p => p.Content == "Bob's private post");
    }

    [Fact]
    public async Task GetFeed_PaginationWorks()
    {
        var user = await CreateUserAsync();
        for (int i = 0; i < 25; i++)
        {
            _db.Posts.Add(new Post
            {
                Id = Guid.NewGuid(),
                AuthorUserId = user.Id,
                Author = user,
                Content = $"Post {i}",
                PostType = PostType.Text,
                Privacy = PrivacyLevel.Friends,
                CreatedAt = DateTime.UtcNow.AddSeconds(i),
                UpdatedAt = DateTime.UtcNow,
            });
        }
        await _db.SaveChangesAsync();

        var page1 = await _sut.GetFeedAsync(user.Id, 1, 10);
        var page2 = await _sut.GetFeedAsync(user.Id, 2, 10);
        var page3 = await _sut.GetFeedAsync(user.Id, 3, 10);

        page1.Items.Should().HaveCount(10);
        page1.HasMore.Should().BeTrue();
        page2.Items.Should().HaveCount(10);
        page3.Items.Should().HaveCount(5);
        page3.HasMore.Should().BeFalse();
    }

    // ── Get single post ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetPost_ExistingPublicPost_ReturnsDto()
    {
        var user = await CreateUserAsync();
        var postId = Guid.NewGuid();
        _db.Posts.Add(new Post
        {
            Id = postId,
            AuthorUserId = user.Id,
            Author = user,
            Content = "Public post",
            PostType = PostType.Text,
            Privacy = PrivacyLevel.Everyone,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var dto = await _sut.GetPostAsync(postId, null);
        dto.Content.Should().Be("Public post");
    }

    [Fact]
    public async Task GetPost_PrivatePost_AnonymousViewer_ThrowsUnauthorized()
    {
        var user = await CreateUserAsync();
        var postId = Guid.NewGuid();
        _db.Posts.Add(new Post
        {
            Id = postId,
            AuthorUserId = user.Id,
            Author = user,
            Content = "Private post",
            PostType = PostType.Text,
            Privacy = PrivacyLevel.OnlyMe,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        await _sut.Invoking(s => s.GetPostAsync(postId, null))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetPost_NotFound_ThrowsKeyNotFound()
    {
        await _sut.Invoking(s => s.GetPostAsync(Guid.NewGuid(), null))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePost_Owner_UpdatesContent()
    {
        var user = await CreateUserAsync();
        var postId = Guid.NewGuid();
        _db.Posts.Add(new Post
        {
            Id = postId,
            AuthorUserId = user.Id,
            Author = user,
            Content = "Old content",
            PostType = PostType.Text,
            Privacy = PrivacyLevel.Friends,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var dto = await _sut.UpdatePostAsync(postId, user.Id, new UpdatePostRequest("New content", null));
        dto.Content.Should().Be("New content");
    }

    [Fact]
    public async Task UpdatePost_NonOwner_ThrowsUnauthorized()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var postId = Guid.NewGuid();
        _db.Posts.Add(new Post
        {
            Id = postId,
            AuthorUserId = alice.Id,
            Author = alice,
            Content = "Alice's post",
            PostType = PostType.Text,
            Privacy = PrivacyLevel.Friends,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        await _sut.Invoking(s => s.UpdatePostAsync(postId, bob.Id, new UpdatePostRequest("Hack", null)))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeletePost_Owner_SoftDeletes()
    {
        var user = await CreateUserAsync();
        var postId = Guid.NewGuid();
        _db.Posts.Add(new Post
        {
            Id = postId,
            AuthorUserId = user.Id,
            Author = user,
            Content = "To be deleted",
            PostType = PostType.Text,
            Privacy = PrivacyLevel.Friends,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        await _sut.DeletePostAsync(postId, user.Id);

        var post = await _db.Posts.FindAsync(postId);
        post!.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeletePost_NonOwner_ThrowsUnauthorized()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var postId = Guid.NewGuid();
        _db.Posts.Add(new Post
        {
            Id = postId,
            AuthorUserId = alice.Id,
            Author = alice,
            Content = "Alice's post",
            PostType = PostType.Text,
            Privacy = PrivacyLevel.Friends,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        await _sut.Invoking(s => s.DeletePostAsync(postId, bob.Id))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── GetFriendIds ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFriendIds_ReturnsAcceptedFriends()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var carol = await CreateUserAsync("Carol");

        await MakeFriendsAsync(alice.Id, bob.Id);
        // carol is not friends with alice
        _db.Friends.Add(new Friend
        {
            Id = Guid.NewGuid(),
            RequesterId = alice.Id,
            AddresseeId = carol.Id,
            Status = FriendStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var ids = await _sut.GetFriendIdsAsync(alice.Id);

        ids.Should().Contain(bob.Id);
        ids.Should().NotContain(carol.Id);
    }
}
