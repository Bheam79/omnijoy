using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

public class NotificationServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly Mock<IHubContextDispatcher> _hub;
    private readonly NotificationService _sut;

    public NotificationServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new OmnijoyDbContext(options);
        _hub = new Mock<IHubContextDispatcher>();
        _sut = new NotificationService(_db, _hub.Object);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<User> CreateUserAsync()
    {
        var u = new User
        {
            Id          = Guid.NewGuid(),
            Email       = $"{Guid.NewGuid()}@test.com",
            DisplayName = "User",
            Gender      = Gender.NotDisclosed,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        };
        _db.Users.Add(u);
        await _db.SaveChangesAsync();
        return u;
    }

    private async Task SetPrefsAsync(Guid userId, Action<NotificationPreferences> mutate)
    {
        var p = new NotificationPreferences { UserId = userId };
        mutate(p);
        _db.NotificationPreferences.Add(p);
        await _db.SaveChangesAsync();
    }

    // ── CreateAsync respects preferences ──────────────────────────────────────

    [Fact]
    public async Task CreateAsync_RecipientHasNoPrefs_DefaultsAllow()
    {
        var actor = await CreateUserAsync();
        var recipient = await CreateUserAsync();

        await _sut.CreateAsync(recipient.Id, NotificationType.PostLike, "ref", actor.Id);

        (await _db.Notifications.CountAsync(n => n.UserId == recipient.Id))
            .Should().Be(1);
        _hub.Verify(
            h => h.SendToUserAsync(recipient.Id, "NotificationReceived", It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_MutedType_SkipsPersistAndPush()
    {
        var actor = await CreateUserAsync();
        var recipient = await CreateUserAsync();
        await SetPrefsAsync(recipient.Id, p => p.LikesOnMyPosts = false);

        await _sut.CreateAsync(recipient.Id, NotificationType.PostLike, "ref", actor.Id);

        (await _db.Notifications.CountAsync(n => n.UserId == recipient.Id))
            .Should().Be(0);
        _hub.Verify(
            h => h.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<object>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_UnrelatedToggleOff_StillDeliversOtherTypes()
    {
        var actor = await CreateUserAsync();
        var recipient = await CreateUserAsync();
        await SetPrefsAsync(recipient.Id, p => p.LikesOnMyPosts = false);

        // EventInvites is not gated by LikesOnMyPosts.
        await _sut.CreateAsync(recipient.Id, NotificationType.EventInvite, "ev", actor.Id);

        (await _db.Notifications.CountAsync(n => n.UserId == recipient.Id))
            .Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_MentionsToggleOff_BlocksAllMentionTypes()
    {
        var actor = await CreateUserAsync();
        var recipient = await CreateUserAsync();
        await SetPrefsAsync(recipient.Id, p => p.Mentions = false);

        await _sut.CreateAsync(recipient.Id, NotificationType.MentionInPost,    "p", actor.Id);
        await _sut.CreateAsync(recipient.Id, NotificationType.MentionInComment, "c", actor.Id);
        await _sut.CreateAsync(recipient.Id, NotificationType.TaggedInPost,     "p", actor.Id);

        (await _db.Notifications.CountAsync(n => n.UserId == recipient.Id))
            .Should().Be(0);
    }

    // ── CreateForManyAsync respects per-recipient preferences ─────────────────

    [Fact]
    public async Task CreateForManyAsync_FiltersMutedRecipients()
    {
        var actor    = await CreateUserAsync();
        var allow    = await CreateUserAsync();
        var deny     = await CreateUserAsync();
        var noPrefs  = await CreateUserAsync();

        await SetPrefsAsync(allow.Id,  p => p.NewPostsFromFriends = true);
        await SetPrefsAsync(deny.Id,   p => p.NewPostsFromFriends = false);

        await _sut.CreateForManyAsync(
            [allow.Id, deny.Id, noPrefs.Id],
            NotificationType.NewPostFromFriend,
            "post-1",
            actor.Id);

        (await _db.Notifications.CountAsync(n => n.UserId == allow.Id)).Should().Be(1);
        (await _db.Notifications.CountAsync(n => n.UserId == deny.Id)).Should().Be(0);
        (await _db.Notifications.CountAsync(n => n.UserId == noPrefs.Id)).Should().Be(1);
    }

    [Fact]
    public async Task CreateForManyAsync_AllRecipientsMuted_NoOp()
    {
        var actor = await CreateUserAsync();
        var a = await CreateUserAsync();
        var b = await CreateUserAsync();

        await SetPrefsAsync(a.Id, p => p.NewPostsFromFriends = false);
        await SetPrefsAsync(b.Id, p => p.NewPostsFromFriends = false);

        await _sut.CreateForManyAsync(
            [a.Id, b.Id],
            NotificationType.NewPostFromFriend,
            "post-1",
            actor.Id);

        (await _db.Notifications.CountAsync()).Should().Be(0);
        _hub.Verify(
            h => h.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<object>()),
            Times.Never);
    }

    // ── Self-notify still suppressed ──────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_SelfNotify_AlwaysSkipped()
    {
        var user = await CreateUserAsync();

        await _sut.CreateAsync(user.Id, NotificationType.PostLike, "ref", user.Id);

        (await _db.Notifications.CountAsync()).Should().Be(0);
    }
}
