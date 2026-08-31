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

public class PostServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly Mock<IMediaStorageService> _storageMock;
    private readonly Mock<IImageProcessingService> _imageProcessorMock;
    private readonly Mock<INotificationService> _notificationsMock;
    private readonly PostService _sut;

    public PostServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new OmnijoyDbContext(options);
        _storageMock = new Mock<IMediaStorageService>();
        _imageProcessorMock = new Mock<IImageProcessingService>();
        _imageProcessorMock
            .Setup(p => p.ProcessImageAsync(It.IsAny<Stream>(), It.IsAny<ImageFolder>()))
            .ReturnsAsync((Stream s, ImageFolder _) => s);
        var privacy = new PrivacyService(_db);
        _notificationsMock = new Mock<INotificationService>();
        _sut = new PostService(
            _db,
            _storageMock.Object,
            privacy,
            null,
            _imageProcessorMock.Object,
            null,
            new MentionResolver(_db),
            _notificationsMock.Object);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<User> CreateUserAsync(string displayName = "Alice", string? urlSlug = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@test.com",
            DisplayName = displayName,
            UrlSlug = urlSlug,
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

    private async Task BlockAsync(Guid blockerId, Guid blockedId)
    {
        _db.Friends.Add(new Friend
        {
            Id = Guid.NewGuid(),
            RequesterId = blockerId,
            AddresseeId = blockedId,
            Status = FriendStatus.Blocked,
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
        // After image processing the filename is always "image.webp"
        _storageMock.Verify(s => s.StoreAsync(It.IsAny<Stream>(), "image.webp", "posts/images"), Times.Once);
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

    [Fact]
    public async Task CreatePost_DuplicateMention_PersistsOnceAndNotifiesWithPostReference()
    {
        var author = await CreateUserAsync("Alice", "alice");
        var mentioned = await CreateUserAsync("Bob", "bob-user");

        var dto = await _sut.CreatePostAsync(
            author.Id,
            new CreatePostRequest("Hi @BOB-user and again @bob-user", "Text", "Everyone", null),
            null);

        var mention = await _db.PostMentions.SingleAsync();
        mention.PostId.Should().Be(dto.Id);
        mention.MentionedUserId.Should().Be(mentioned.Id);
        mention.MatchedSlug.Should().Be("bob-user");
        dto.Mentions.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            MatchedSlug = "bob-user",
            UserId = mentioned.Id,
            DisplayName = "Bob",
            UrlSlug = "bob-user",
        });
        _notificationsMock.Verify(n => n.CreateAsync(
            mentioned.Id,
            NotificationType.MentionInPost,
            dto.Id.ToString(),
            author.Id), Times.Once);
    }

    [Fact]
    public async Task CreatePost_SelfMention_IsPersistedButNotNotified()
    {
        var author = await CreateUserAsync("Alice", "alice");

        var dto = await _sut.CreatePostAsync(
            author.Id,
            new CreatePostRequest("Note to @alice", "Text", "OnlyMe", null),
            null);

        var mention = await _db.PostMentions.SingleAsync();
        mention.PostId.Should().Be(dto.Id);
        mention.MentionedUserId.Should().Be(author.Id);
        _notificationsMock.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<Guid?>()),
            Times.Never);
    }

    [Fact]
    public async Task CreatePost_BlockedMention_LeavesTextButDoesNotPersistOrNotify()
    {
        var author = await CreateUserAsync("Alice", "alice");
        var blocked = await CreateUserAsync("Bob", "blocked-bob");
        await BlockAsync(blocked.Id, author.Id);

        var dto = await _sut.CreatePostAsync(
            author.Id,
            new CreatePostRequest("Hello @blocked-bob", "Text", "Everyone", null),
            null);

        dto.Content.Should().Be("Hello @blocked-bob");
        dto.Mentions.Should().BeEmpty();
        (await _db.PostMentions.CountAsync()).Should().Be(0);
        _notificationsMock.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<Guid?>()),
            Times.Never);
    }

    [Fact]
    public async Task CreatePost_OverMentionLimit_IsRejectedAtomically()
    {
        var author = await CreateUserAsync();
        var content = string.Join(' ', Enumerable.Range(0, 11).Select(i => $"@user{i}"));

        await _sut.Invoking(s => s.CreatePostAsync(
                author.Id,
                new CreatePostRequest(content, "Text", "Everyone", null),
                null))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*10 distinct users*");

        (await _db.Posts.CountAsync()).Should().Be(0);
        (await _db.PostMentions.CountAsync()).Should().Be(0);
        _notificationsMock.VerifyNoOtherCalls();
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
        feed.Items[0].Post!.Content.Should().Be("My post");
        feed.Items[0].ItemType.Should().Be("Post");
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

        feed.Items.Should().Contain(item => item.Post != null && item.Post.Content == "Bob's public post");
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

        feed.Items.Should().NotContain(item => item.Post != null && item.Post.Content == "Bob's private post");
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
    public async Task PostDtoQueries_ReturnMatchedSlugWithCurrentMentionedUserProfile()
    {
        var author = await CreateUserAsync("Alice", "alice");
        var mentioned = await CreateUserAsync("Bob Before", "bob-old");
        var created = await _sut.CreatePostAsync(
            author.Id,
            new CreatePostRequest("Hi @bob-old, again @BOB-OLD", "Text", "Everyone", null),
            null);

        mentioned.DisplayName = "Bob After";
        mentioned.UrlSlug = "bob-current";
        await _db.SaveChangesAsync();

        var detail = await _sut.GetPostAsync(created.Id, author.Id);
        var feed = await _sut.GetFeedAsync(author.Id, 1, 20);

        detail.Mentions.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            MatchedSlug = "bob-old",
            UserId = mentioned.Id,
            DisplayName = "Bob After",
            UrlSlug = "bob-current",
        });
        feed.Items.Single(item => item.Post?.Id == created.Id)
            .Post!.Mentions.Should().BeEquivalentTo(detail.Mentions);

        var updated = await _sut.UpdatePostAsync(
            created.Id,
            author.Id,
            new UpdatePostRequest("Still using @bob-current", null));
        updated.Mentions.Should().ContainSingle().Which.MatchedSlug.Should().Be("bob-current");
        updated.Mentions![0].UrlSlug.Should().Be("bob-current");
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

    [Fact]
    public async Task UpdatePost_DiffsMentions_AndOnlyNotifiesNewRecipients()
    {
        var author = await CreateUserAsync("Alice", "alice");
        var bob = await CreateUserAsync("Bob", "bob-user");
        var carol = await CreateUserAsync("Carol", "carol-user");
        var post = await _sut.CreatePostAsync(
            author.Id,
            new CreatePostRequest("Hi @bob-user", "Text", "Everyone", null),
            null);
        _notificationsMock.Invocations.Clear();

        await _sut.UpdatePostAsync(
            post.Id,
            author.Id,
            new UpdatePostRequest("Still @bob-user, welcome @carol-user", null));

        (await _db.PostMentions.Where(m => m.PostId == post.Id).Select(m => m.MentionedUserId).ToListAsync())
            .Should().BeEquivalentTo([bob.Id, carol.Id]);
        _notificationsMock.Verify(n => n.CreateAsync(
            carol.Id, NotificationType.MentionInPost, post.Id.ToString(), author.Id), Times.Once);
        _notificationsMock.Verify(n => n.CreateAsync(
            bob.Id, NotificationType.MentionInPost, It.IsAny<string>(), author.Id), Times.Never);

        _notificationsMock.Invocations.Clear();
        await _sut.UpdatePostAsync(
            post.Id,
            author.Id,
            new UpdatePostRequest("Only unchanged @bob-user remains", null));

        (await _db.PostMentions.Where(m => m.PostId == post.Id).Select(m => m.MentionedUserId).ToListAsync())
            .Should().Equal(bob.Id);
        _notificationsMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdatePost_OverMentionLimit_LeavesContentAndMentionsUnchanged()
    {
        var author = await CreateUserAsync("Alice", "alice");
        var bob = await CreateUserAsync("Bob", "bob-user");
        var post = await _sut.CreatePostAsync(
            author.Id,
            new CreatePostRequest("Original @bob-user", "Text", "Everyone", null),
            null);
        _notificationsMock.Invocations.Clear();
        var overLimit = string.Join(' ', Enumerable.Range(0, 11).Select(i => $"@user{i}"));

        await _sut.Invoking(s => s.UpdatePostAsync(
                post.Id,
                author.Id,
                new UpdatePostRequest(overLimit, null)))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*10 distinct users*");

        (await _db.Posts.FindAsync(post.Id))!.Content.Should().Be("Original @bob-user");
        (await _db.PostMentions.SingleAsync(m => m.PostId == post.Id)).MentionedUserId.Should().Be(bob.Id);
        _notificationsMock.VerifyNoOtherCalls();
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
