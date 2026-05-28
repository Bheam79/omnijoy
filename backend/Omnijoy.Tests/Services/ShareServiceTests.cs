using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

public class ShareServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly ShareService _sut;

    public ShareServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db  = new OmnijoyDbContext(options);
        _sut = new ShareService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<User> CreateUserAsync(string displayName = "Alice")
    {
        var user = new User
        {
            Id          = Guid.NewGuid(),
            Email       = $"{Guid.NewGuid()}@test.com",
            DisplayName = displayName,
            Gender      = Gender.NotDisclosed,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<Post> CreatePublicPostAsync(User author, string content = "Hello!")
    {
        var post = new Post
        {
            Id           = Guid.NewGuid(),
            AuthorUserId = author.Id,
            Author       = author,
            Content      = content,
            PostType     = PostType.Text,
            Privacy      = PrivacyLevel.Everyone,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        };
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        return post;
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

    // ── CreateShareAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateShare_OwnWall_ReturnsDto()
    {
        var alice = await CreateUserAsync("Alice");
        var post  = await CreatePublicPostAsync(alice);

        var dto = await _sut.CreateShareAsync(
            alice.Id,
            post.Id,
            new CreateShareRequest("OwnWall", null, "Great post!"));

        dto.Should().NotBeNull();
        dto.Sharer.Id.Should().Be(alice.Id);
        dto.TargetType.Should().Be("OwnWall");
        dto.Message.Should().Be("Great post!");
        dto.OriginalPost.Id.Should().Be(post.Id);
        dto.TargetId.Should().BeNull();
    }

    [Fact]
    public async Task CreateShare_OwnWall_NoMessage_MessageIsNull()
    {
        var alice = await CreateUserAsync("Alice");
        var post  = await CreatePublicPostAsync(alice);

        var dto = await _sut.CreateShareAsync(
            alice.Id,
            post.Id,
            new CreateShareRequest("OwnWall", null, null));

        dto.Message.Should().BeNull();
    }

    [Fact]
    public async Task CreateShare_FriendWall_WithAcceptedFriend_ReturnsDto()
    {
        var alice = await CreateUserAsync("Alice");
        var bob   = await CreateUserAsync("Bob");
        await MakeFriendsAsync(alice.Id, bob.Id);
        var post = await CreatePublicPostAsync(alice);

        var dto = await _sut.CreateShareAsync(
            alice.Id,
            post.Id,
            new CreateShareRequest("FriendWall", bob.Id, "Sharing to Bob"));

        dto.TargetType.Should().Be("FriendWall");
        dto.TargetId.Should().Be(bob.Id);
    }

    [Fact]
    public async Task CreateShare_FriendWall_NonFriend_ThrowsUnauthorized()
    {
        var alice   = await CreateUserAsync("Alice");
        var charlie = await CreateUserAsync("Charlie");
        var post = await CreatePublicPostAsync(alice);

        await _sut.Invoking(s => s.CreateShareAsync(
                alice.Id,
                post.Id,
                new CreateShareRequest("FriendWall", charlie.Id, null)))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*friend*");
    }

    [Fact]
    public async Task CreateShare_FriendWall_MissingTargetId_ThrowsArgumentException()
    {
        var alice = await CreateUserAsync("Alice");
        var post  = await CreatePublicPostAsync(alice);

        await _sut.Invoking(s => s.CreateShareAsync(
                alice.Id,
                post.Id,
                new CreateShareRequest("FriendWall", null, null)))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*TargetId*");
    }

    [Fact]
    public async Task CreateShare_OnlyMePost_ByOtherUser_ThrowsUnauthorized()
    {
        var alice = await CreateUserAsync("Alice");
        var bob   = await CreateUserAsync("Bob");

        var privatePost = new Post
        {
            Id           = Guid.NewGuid(),
            AuthorUserId = alice.Id,
            Author       = alice,
            Content      = "Private",
            PostType     = PostType.Text,
            Privacy      = PrivacyLevel.OnlyMe,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        };
        _db.Posts.Add(privatePost);
        await _db.SaveChangesAsync();

        await _sut.Invoking(s => s.CreateShareAsync(
                bob.Id,
                privatePost.Id,
                new CreateShareRequest("OwnWall", null, null)))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task CreateShare_PostNotFound_ThrowsKeyNotFound()
    {
        var alice = await CreateUserAsync("Alice");

        await _sut.Invoking(s => s.CreateShareAsync(
                alice.Id,
                Guid.NewGuid(),
                new CreateShareRequest("OwnWall", null, null)))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task CreateShare_InvalidTargetType_ThrowsArgumentException()
    {
        var alice = await CreateUserAsync("Alice");
        var post  = await CreatePublicPostAsync(alice);

        await _sut.Invoking(s => s.CreateShareAsync(
                alice.Id,
                post.Id,
                new CreateShareRequest("WeirdTarget", null, null)))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*TargetType*");
    }

    [Fact]
    public async Task CreateShare_CompanyPage_WithFollower_ReturnsDto()
    {
        var alice = await CreateUserAsync("Alice");
        var post  = await CreatePublicPostAsync(alice);

        var page = new CompanyPage
        {
            Id              = Guid.NewGuid(),
            Name            = "TestCo",
            CreatedByUserId = alice.Id,
            CreatedByUser   = alice,
            CreatedAt       = DateTime.UtcNow,
        };
        _db.CompanyPages.Add(page);

        _db.CompanyPageFollows.Add(new CompanyPageFollow
        {
            CompanyPageId = page.Id,
            UserId        = alice.Id,
            CompanyPage   = page,
            User          = alice,
            CreatedAt     = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var dto = await _sut.CreateShareAsync(
            alice.Id,
            post.Id,
            new CreateShareRequest("CompanyPage", page.Id, null));

        dto.TargetType.Should().Be("CompanyPage");
        dto.TargetId.Should().Be(page.Id);
    }

    // ── GetShareRecipientIdsAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetShareRecipientIds_OwnWall_IncludesSharerAndFriends()
    {
        var alice = await CreateUserAsync("Alice");
        var bob   = await CreateUserAsync("Bob");
        await MakeFriendsAsync(alice.Id, bob.Id);
        var post = await CreatePublicPostAsync(alice);

        var dto = await _sut.CreateShareAsync(
            alice.Id, post.Id, new CreateShareRequest("OwnWall", null, null));

        var recipients = await _sut.GetShareRecipientIdsAsync(alice.Id, dto);

        recipients.Should().Contain(alice.Id);
        recipients.Should().Contain(bob.Id);
    }

    [Fact]
    public async Task GetShareRecipientIds_FriendWall_IncludesTargetUser()
    {
        var alice = await CreateUserAsync("Alice");
        var bob   = await CreateUserAsync("Bob");
        await MakeFriendsAsync(alice.Id, bob.Id);
        var post = await CreatePublicPostAsync(alice);

        var dto = await _sut.CreateShareAsync(
            alice.Id, post.Id, new CreateShareRequest("FriendWall", bob.Id, null));

        var recipients = await _sut.GetShareRecipientIdsAsync(alice.Id, dto);

        recipients.Should().Contain(bob.Id);
        recipients.Should().Contain(alice.Id);
    }
}
