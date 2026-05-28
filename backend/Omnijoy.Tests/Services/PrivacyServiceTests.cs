using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

public class PrivacyServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly PrivacyService _sut;

    public PrivacyServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new OmnijoyDbContext(options);
        _sut = new PrivacyService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> CreateUserAsync(
        PrivacyLevel whoCanSeeProfile   = PrivacyLevel.Everyone,
        PrivacyLevel whoCanSeePosts     = PrivacyLevel.Friends,
        PrivacyLevel whoCanSendMessages = PrivacyLevel.Friends,
        PrivacyLevel whoCanSeeFriendList = PrivacyLevel.Friends)
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id          = userId,
            Email       = $"{userId}@test.com",
            DisplayName = "Test",
            Gender      = Gender.NotDisclosed,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        };
        _db.Users.Add(user);
        _db.UserPrivacySettings.Add(new UserPrivacySettings
        {
            UserId              = userId,
            WhoCanSeeProfile    = whoCanSeeProfile,
            WhoCanSeePosts      = whoCanSeePosts,
            WhoCanSendMessages  = whoCanSendMessages,
            WhoCanSeeFriendList = whoCanSeeFriendList,
        });
        await _db.SaveChangesAsync();
        return userId;
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

    // ── AreNotBlockedAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task AreNotBlocked_WhenNoRelationship_ReturnsTrue()
    {
        var a = await CreateUserAsync();
        var b = await CreateUserAsync();

        var result = await _sut.AreNotBlockedAsync(a, b);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AreNotBlocked_WhenABlocksB_ReturnsFalse()
    {
        var a = await CreateUserAsync();
        var b = await CreateUserAsync();
        await BlockAsync(a, b);

        var result = await _sut.AreNotBlockedAsync(a, b);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AreNotBlocked_WhenBBlocksA_ReturnsFalse()
    {
        var a = await CreateUserAsync();
        var b = await CreateUserAsync();
        await BlockAsync(b, a);

        // Check from A's perspective
        var result = await _sut.AreNotBlockedAsync(a, b);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AreNotBlocked_WhenFriends_ReturnsTrue()
    {
        var a = await CreateUserAsync();
        var b = await CreateUserAsync();
        await MakeFriendsAsync(a, b);

        var result = await _sut.AreNotBlockedAsync(a, b);

        result.Should().BeTrue();
    }

    // ── AreFriendsAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task AreFriends_WhenFriends_ReturnsTrue()
    {
        var a = await CreateUserAsync();
        var b = await CreateUserAsync();
        await MakeFriendsAsync(a, b);

        var result = await _sut.AreFriendsAsync(a, b);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AreFriends_WhenNotFriends_ReturnsFalse()
    {
        var a = await CreateUserAsync();
        var b = await CreateUserAsync();

        var result = await _sut.AreFriendsAsync(a, b);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AreFriends_BothDirections()
    {
        var a = await CreateUserAsync();
        var b = await CreateUserAsync();
        await MakeFriendsAsync(b, a); // b sent the request

        var result = await _sut.AreFriendsAsync(a, b);

        result.Should().BeTrue();
    }

    // ── CanViewProfileAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task CanViewProfile_OwnerAlwaysCanView()
    {
        var userId = await CreateUserAsync(whoCanSeeProfile: PrivacyLevel.OnlyMe);

        var result = await _sut.CanViewProfileAsync(userId, userId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanViewProfile_Everyone_AnyoneCanView()
    {
        var owner   = await CreateUserAsync(whoCanSeeProfile: PrivacyLevel.Everyone);
        var viewer  = await CreateUserAsync();

        var result = await _sut.CanViewProfileAsync(owner, viewer);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanViewProfile_Everyone_AnonymousCanView()
    {
        var owner = await CreateUserAsync(whoCanSeeProfile: PrivacyLevel.Everyone);

        var result = await _sut.CanViewProfileAsync(owner, null);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanViewProfile_Friends_FriendCanView()
    {
        var owner   = await CreateUserAsync(whoCanSeeProfile: PrivacyLevel.Friends);
        var viewer  = await CreateUserAsync();
        await MakeFriendsAsync(owner, viewer);

        var result = await _sut.CanViewProfileAsync(owner, viewer);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanViewProfile_Friends_StrangerCannotView()
    {
        var owner   = await CreateUserAsync(whoCanSeeProfile: PrivacyLevel.Friends);
        var stranger = await CreateUserAsync();

        var result = await _sut.CanViewProfileAsync(owner, stranger);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanViewProfile_OnlyMe_NobodyElseCanView()
    {
        var owner   = await CreateUserAsync(whoCanSeeProfile: PrivacyLevel.OnlyMe);
        var friend  = await CreateUserAsync();
        await MakeFriendsAsync(owner, friend);

        var result = await _sut.CanViewProfileAsync(owner, friend);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanViewProfile_BlockedUser_CannotView()
    {
        // Even with "Everyone" profile, blocked users cannot see the profile
        var owner   = await CreateUserAsync(whoCanSeeProfile: PrivacyLevel.Everyone);
        var viewer  = await CreateUserAsync();
        await BlockAsync(owner, viewer); // owner blocked viewer

        var result = await _sut.CanViewProfileAsync(owner, viewer);

        result.Should().BeFalse();
    }

    // ── CanViewPostsAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task CanViewPosts_Everyone_StrangerCanView()
    {
        var owner   = await CreateUserAsync(whoCanSeePosts: PrivacyLevel.Everyone);
        var viewer  = await CreateUserAsync();

        var result = await _sut.CanViewPostsAsync(owner, viewer);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanViewPosts_Friends_FriendCanView()
    {
        var owner  = await CreateUserAsync(whoCanSeePosts: PrivacyLevel.Friends);
        var viewer = await CreateUserAsync();
        await MakeFriendsAsync(owner, viewer);

        var result = await _sut.CanViewPostsAsync(owner, viewer);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanViewPosts_Friends_StrangerCannotView()
    {
        var owner   = await CreateUserAsync(whoCanSeePosts: PrivacyLevel.Friends);
        var stranger = await CreateUserAsync();

        var result = await _sut.CanViewPostsAsync(owner, stranger);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanViewPosts_BlockedUser_CannotView()
    {
        var owner   = await CreateUserAsync(whoCanSeePosts: PrivacyLevel.Everyone);
        var viewer  = await CreateUserAsync();
        await BlockAsync(viewer, owner); // viewer blocked owner

        var result = await _sut.CanViewPostsAsync(owner, viewer);

        result.Should().BeFalse();
    }

    // ── CanSendMessagesAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task CanSendMessages_Everyone_AnyoneCanMessage()
    {
        var target  = await CreateUserAsync(whoCanSendMessages: PrivacyLevel.Everyone);
        var sender  = await CreateUserAsync();

        var result = await _sut.CanSendMessagesAsync(target, sender);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanSendMessages_Friends_FriendCanMessage()
    {
        var target = await CreateUserAsync(whoCanSendMessages: PrivacyLevel.Friends);
        var sender = await CreateUserAsync();
        await MakeFriendsAsync(target, sender);

        var result = await _sut.CanSendMessagesAsync(target, sender);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanSendMessages_Friends_StrangerCannotMessage()
    {
        var target   = await CreateUserAsync(whoCanSendMessages: PrivacyLevel.Friends);
        var stranger = await CreateUserAsync();

        var result = await _sut.CanSendMessagesAsync(target, stranger);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanSendMessages_OnlyMe_NobodyCanMessage()
    {
        var target = await CreateUserAsync(whoCanSendMessages: PrivacyLevel.OnlyMe);
        var friend = await CreateUserAsync();
        await MakeFriendsAsync(target, friend);

        var result = await _sut.CanSendMessagesAsync(target, friend);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanSendMessages_BlockedUser_CannotMessage()
    {
        var target = await CreateUserAsync(whoCanSendMessages: PrivacyLevel.Everyone);
        var sender = await CreateUserAsync();
        await BlockAsync(target, sender); // target blocked sender

        var result = await _sut.CanSendMessagesAsync(target, sender);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanSendMessages_CannotMessageYourself()
    {
        var userId = await CreateUserAsync(whoCanSendMessages: PrivacyLevel.Everyone);

        var result = await _sut.CanSendMessagesAsync(userId, userId);

        result.Should().BeFalse();
    }

    // ── CanViewFriendListAsync ────────────────────────────────────────────────

    [Fact]
    public async Task CanViewFriendList_OwnerAlwaysCanView()
    {
        var userId = await CreateUserAsync(whoCanSeeFriendList: PrivacyLevel.OnlyMe);

        var result = await _sut.CanViewFriendListAsync(userId, userId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanViewFriendList_Everyone_StrangerCanView()
    {
        var owner   = await CreateUserAsync(whoCanSeeFriendList: PrivacyLevel.Everyone);
        var viewer  = await CreateUserAsync();

        var result = await _sut.CanViewFriendListAsync(owner, viewer);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanViewFriendList_Friends_FriendCanView()
    {
        var owner  = await CreateUserAsync(whoCanSeeFriendList: PrivacyLevel.Friends);
        var viewer = await CreateUserAsync();
        await MakeFriendsAsync(owner, viewer);

        var result = await _sut.CanViewFriendListAsync(owner, viewer);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanViewFriendList_Friends_StrangerCannotView()
    {
        var owner    = await CreateUserAsync(whoCanSeeFriendList: PrivacyLevel.Friends);
        var stranger = await CreateUserAsync();

        var result = await _sut.CanViewFriendListAsync(owner, stranger);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanViewFriendList_OnlyMe_FriendCannotView()
    {
        var owner  = await CreateUserAsync(whoCanSeeFriendList: PrivacyLevel.OnlyMe);
        var friend = await CreateUserAsync();
        await MakeFriendsAsync(owner, friend);

        var result = await _sut.CanViewFriendListAsync(owner, friend);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanViewFriendList_BlockedUser_CannotView()
    {
        var owner  = await CreateUserAsync(whoCanSeeFriendList: PrivacyLevel.Everyone);
        var viewer = await CreateUserAsync();
        await BlockAsync(owner, viewer);

        var result = await _sut.CanViewFriendListAsync(owner, viewer);

        result.Should().BeFalse();
    }
}
