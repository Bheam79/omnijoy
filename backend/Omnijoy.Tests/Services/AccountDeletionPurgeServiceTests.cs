using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AccountDeletionPurgeService"/>.
///
/// EF Core InMemory does NOT enforce FK constraints, so we don't exercise the
/// Restrict/Cascade ordering here — we only verify the resulting DB state
/// (the expected rows are gone). Real-DB FK ordering is exercised by E2E /
/// integration tests against MariaDB.
///
/// <see cref="AccountDeletionPurgeService.PurgeOnceAsync"/> is exposed as
/// <c>internal</c> via <c>InternalsVisibleTo("Omnijoy.Tests")</c> in
/// <c>Omnijoy.Infrastructure.csproj</c>.
/// </summary>
public class AccountDeletionPurgeServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;

    public AccountDeletionPurgeServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new OmnijoyDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private AccountDeletionPurgeService MakeSut(
        OmnijoyDbContext db,
        IMediaStorageService? storage = null,
        int graceDays = 30)
    {
        var storageMock = storage ?? Mock.Of<IMediaStorageService>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Account:DeletionGraceDays"] = graceDays.ToString(),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<OmnijoyDbContext>(db);
        services.AddSingleton<IMediaStorageService>(storageMock);
        services.AddSingleton<IConfiguration>(config);
        var sp = services.BuildServiceProvider();

        return new AccountDeletionPurgeService(
            sp,
            NullLogger<AccountDeletionPurgeService>.Instance);
    }

    private async Task<User> CreateUserAsync(
        DateTime? deletionScheduledAt = null,
        string? avatarUrl = null,
        string? coverUrl = null)
    {
        var user = new User
        {
            Id                  = Guid.NewGuid(),
            Email               = $"{Guid.NewGuid()}@test.com",
            DisplayName         = "Test User",
            Gender              = Gender.NotDisclosed,
            CreatedAt           = DateTime.UtcNow,
            UpdatedAt           = DateTime.UtcNow,
            DeletionScheduledAt = deletionScheduledAt,
            AvatarUrl           = avatarUrl,
            CoverUrl            = coverUrl,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<Post> CreatePostAsync(Guid authorId, string? mediaUrl = null)
    {
        var post = new Post
        {
            Id           = Guid.NewGuid(),
            AuthorUserId = authorId,
            Content      = "Some post",
            PostType     = PostType.Text,
            Privacy      = PrivacyLevel.Everyone,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        };
        _db.Posts.Add(post);

        if (mediaUrl is not null)
        {
            _db.PostMedia.Add(new PostMedia
            {
                Id        = Guid.NewGuid(),
                PostId    = post.Id,
                MediaType = MediaType.Image,
                Url       = mediaUrl,
                Order     = 0,
            });
        }

        await _db.SaveChangesAsync();
        return post;
    }

    // ── 1. User not yet past grace period — not deleted ──────────────────────

    [Fact]
    public async Task UserNotYetPastGracePeriod_NotDeleted()
    {
        // Scheduled 1 day ago, grace is 30 days → not yet eligible.
        var user = await CreateUserAsync(DateTime.UtcNow.AddDays(-1));

        var sut = MakeSut(_db, graceDays: 30);
        await sut.PurgeOnceAsync(CancellationToken.None);

        var stillThere = await _db.Users.FindAsync(user.Id);
        stillThere.Should().NotBeNull();
    }

    // ── 2. User past grace period — deleted with dependent rows ──────────────

    [Fact]
    public async Task UserPastGracePeriod_DeletedWithDependentRows()
    {
        var doomed = await CreateUserAsync(DateTime.UtcNow.AddDays(-31));
        var friend = await CreateUserAsync();

        // Their own post
        var ownPost = await CreatePostAsync(doomed.Id);

        // A SharedPost authored by `doomed`
        _db.SharedPosts.Add(new SharedPost
        {
            Id             = Guid.NewGuid(),
            OriginalPostId = ownPost.Id,
            SharerId       = doomed.Id,
            TargetType     = ShareTargetType.OwnWall,
            CreatedAt      = DateTime.UtcNow,
        });

        // A comment by `doomed` on someone else's post
        var otherPost = await CreatePostAsync(friend.Id);
        _db.Comments.Add(new Comment
        {
            Id        = Guid.NewGuid(),
            PostId    = otherPost.Id,
            AuthorId  = doomed.Id,
            Content   = "nice",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        // Friendship between the two
        _db.Friends.Add(new Friend
        {
            Id          = Guid.NewGuid(),
            RequesterId = doomed.Id,
            AddresseeId = friend.Id,
            Status      = FriendStatus.Accepted,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        });

        // Notification targeting `doomed` (cascades on User delete)
        _db.Notifications.Add(new Notification
        {
            Id        = Guid.NewGuid(),
            UserId    = doomed.Id,
            Type      = NotificationType.FriendRequest,
            CreatedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();

        var sut = MakeSut(_db, graceDays: 30);
        await sut.PurgeOnceAsync(CancellationToken.None);

        // User row gone
        (await _db.Users.FindAsync(doomed.Id)).Should().BeNull();

        // Their posts gone
        (await _db.Posts.AnyAsync(p => p.AuthorUserId == doomed.Id)).Should().BeFalse();

        // Their friend rows gone (both directions)
        (await _db.Friends.AnyAsync(f =>
            f.RequesterId == doomed.Id || f.AddresseeId == doomed.Id)).Should().BeFalse();

        // Their notifications gone
        (await _db.Notifications.AnyAsync(n => n.UserId == doomed.Id)).Should().BeFalse();

        // Their comments on other users' posts gone
        (await _db.Comments.AnyAsync(c => c.AuthorId == doomed.Id)).Should().BeFalse();

        // Their SharedPosts gone
        (await _db.SharedPosts.AnyAsync(sp => sp.SharerId == doomed.Id)).Should().BeFalse();

        // The friend's own user row survives
        (await _db.Users.FindAsync(friend.Id)).Should().NotBeNull();
    }

    // ── 3. User with no deletion scheduled — not deleted ─────────────────────

    [Fact]
    public async Task UserNoDeletionScheduled_NotDeleted()
    {
        var user = await CreateUserAsync(deletionScheduledAt: null);

        var sut = MakeSut(_db, graceDays: 30);
        await sut.PurgeOnceAsync(CancellationToken.None);

        var stillThere = await _db.Users.FindAsync(user.Id);
        stillThere.Should().NotBeNull();
    }

    // ── 4. Exception during one user does not abort the batch ────────────────

    [Fact]
    public async Task ExceptionInOneUser_DoesNotAbortBatch()
    {
        // Two users past grace, each with an avatar URL. The storage mock
        // throws on user B's avatar — the per-URL try/catch inside
        // PurgeUserAsync swallows it and the batch must still complete user
        // A. We additionally verify the loop iterated past the throwing call
        // (it called both DeleteAsync's), proving one user's failure didn't
        // abort the batch.
        var userA = await CreateUserAsync(
            DateTime.UtcNow.AddDays(-31), avatarUrl: "https://example/a.png");
        var userB = await CreateUserAsync(
            DateTime.UtcNow.AddDays(-31), avatarUrl: "https://example/b.png");

        var storage = new Mock<IMediaStorageService>();
        storage.Setup(s => s.DeleteAsync("https://example/a.png"))
               .Returns(Task.CompletedTask);
        storage.Setup(s => s.DeleteAsync("https://example/b.png"))
               .ThrowsAsync(new InvalidOperationException("storage down for B"));

        var sut = MakeSut(_db, storage.Object, graceDays: 30);

        Func<Task> act = () => sut.PurgeOnceAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();

        (await _db.Users.FindAsync(userA.Id)).Should().BeNull();
        (await _db.Users.FindAsync(userB.Id)).Should().BeNull();

        storage.Verify(s => s.DeleteAsync("https://example/a.png"), Times.Once);
        storage.Verify(s => s.DeleteAsync("https://example/b.png"), Times.Once);
    }

    // ── 5. Media files deleted when user purged ──────────────────────────────

    [Fact]
    public async Task MediaFilesDeleted_WhenUserPurged()
    {
        var user = await CreateUserAsync(
            deletionScheduledAt: DateTime.UtcNow.AddDays(-31),
            avatarUrl:           "https://example/avatar.png");

        await CreatePostAsync(user.Id, mediaUrl: "https://example/post-media.jpg");

        var storage = new Mock<IMediaStorageService>();
        storage.Setup(s => s.DeleteAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        var sut = MakeSut(_db, storage.Object, graceDays: 30);
        await sut.PurgeOnceAsync(CancellationToken.None);

        storage.Verify(s => s.DeleteAsync("https://example/avatar.png"),    Times.Once);
        storage.Verify(s => s.DeleteAsync("https://example/post-media.jpg"), Times.Once);
    }

    // ── Interval constant ────────────────────────────────────────────────────

    [Fact]
    public void PurgeInterval_Is24Hours()
    {
        AccountDeletionPurgeService.PurgeInterval.Should().Be(TimeSpan.FromHours(24));
    }

}
