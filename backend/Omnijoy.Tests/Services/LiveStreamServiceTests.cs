using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Omnijoy.Core.DTOs.Live;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

public class LiveStreamServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly LiveStreamService _sut;

    public LiveStreamServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new OmnijoyDbContext(options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Live:RtmpHost"] = "rtmp.test",
                ["Live:RtmpPort"] = "1935",
            })
            .Build();

        _sut = new LiveStreamService(_db, config);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<User> CreateUserAsync(string name = "Alice")
    {
        var user = new User
        {
            Id          = Guid.NewGuid(),
            Email       = $"{Guid.NewGuid()}@test.com",
            DisplayName = name,
            Gender      = Gender.NotDisclosed,
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

    private async Task<LiveStream> CreateLiveStreamAsync(
        User host,
        LiveStreamStatus status = LiveStreamStatus.Live,
        PrivacyLevel privacy = PrivacyLevel.Everyone)
    {
        var stream = new LiveStream
        {
            Id          = Guid.NewGuid(),
            HostUserId  = host.Id,
            HostUser    = host,
            Title       = "Test Stream",
            Status      = status,
            Privacy     = privacy,
            StreamKey   = Guid.NewGuid().ToString("N"),
            StartedAt   = DateTime.UtcNow,
        };
        _db.LiveStreams.Add(stream);
        await _db.SaveChangesAsync();
        return stream;
    }

    // ── StartStreamAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task StartStream_ValidRequest_CreatesStreamAndReturnsResponse()
    {
        var host = await CreateUserAsync();

        var response = await _sut.StartStreamAsync(host.Id, new StartStreamRequest("My Stream", "Everyone"));

        response.Should().NotBeNull();
        response.Title.Should().Be("My Stream");
        response.StreamKey.Should().NotBeNullOrWhiteSpace();
        response.IngestUrl.Should().Contain("rtmp.test");
        response.HlsUrl.Should().Contain(response.Id.ToString());

        var stored = await _db.LiveStreams.FirstAsync(ls => ls.Id == response.Id);
        stored.HostUserId.Should().Be(host.Id);
        stored.Status.Should().Be(LiveStreamStatus.Live);
        stored.Privacy.Should().Be(PrivacyLevel.Everyone);
    }

    [Fact]
    public async Task StartStream_FriendsPrivacy_Allowed()
    {
        var host = await CreateUserAsync();

        var response = await _sut.StartStreamAsync(host.Id, new StartStreamRequest("Friends Only", "Friends"));

        response.Should().NotBeNull();
        var stored = await _db.LiveStreams.FirstAsync(ls => ls.Id == response.Id);
        stored.Privacy.Should().Be(PrivacyLevel.Friends);
    }

    [Fact]
    public async Task StartStream_EmptyTitle_ThrowsArgumentException()
    {
        var host = await CreateUserAsync();

        await _sut.Invoking(s => s.StartStreamAsync(host.Id, new StartStreamRequest("", "Everyone")))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*title*");
    }

    [Fact]
    public async Task StartStream_WhitespaceTitle_ThrowsArgumentException()
    {
        var host = await CreateUserAsync();

        await _sut.Invoking(s => s.StartStreamAsync(host.Id, new StartStreamRequest("   ", "Everyone")))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task StartStream_InvalidPrivacy_ThrowsArgumentException()
    {
        var host = await CreateUserAsync();

        await _sut.Invoking(s => s.StartStreamAsync(host.Id, new StartStreamRequest("Title", "BadPrivacy")))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Privacy*");
    }

    [Fact]
    public async Task StartStream_OnlyMePrivacy_ThrowsArgumentException()
    {
        var host = await CreateUserAsync();

        await _sut.Invoking(s => s.StartStreamAsync(host.Id, new StartStreamRequest("Title", "OnlyMe")))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Everyone*");
    }

    [Fact]
    public async Task StartStream_EndsExistingLiveStream()
    {
        var host = await CreateUserAsync();
        var existingStream = await CreateLiveStreamAsync(host, LiveStreamStatus.Live);

        await _sut.StartStreamAsync(host.Id, new StartStreamRequest("New Stream", "Everyone"));

        var old = await _db.LiveStreams.FirstAsync(ls => ls.Id == existingStream.Id);
        old.Status.Should().Be(LiveStreamStatus.Ended);
        old.EndedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task StartStream_UsesDefaultRtmpHostWhenNotConfigured()
    {
        var config = new ConfigurationBuilder().Build(); // no Live: section
        var sut = new LiveStreamService(_db, config);
        var host = await CreateUserAsync();

        var response = await sut.StartStreamAsync(host.Id, new StartStreamRequest("Default", "Everyone"));

        response.IngestUrl.Should().Contain("localhost");
        response.IngestUrl.Should().Contain("1935");
    }

    // ── EndStreamAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task EndStream_ValidHost_EndsStream()
    {
        var host = await CreateUserAsync();
        var stream = await CreateLiveStreamAsync(host);

        await _sut.EndStreamAsync(stream.Id, host.Id);

        var stored = await _db.LiveStreams.FirstAsync(ls => ls.Id == stream.Id);
        stored.Status.Should().Be(LiveStreamStatus.Ended);
        stored.EndedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task EndStream_NotFound_ThrowsKeyNotFoundException()
    {
        var host = await CreateUserAsync();

        await _sut.Invoking(s => s.EndStreamAsync(Guid.NewGuid(), host.Id))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task EndStream_NonHost_ThrowsUnauthorized()
    {
        var host    = await CreateUserAsync("Host");
        var other   = await CreateUserAsync("Other");
        var stream  = await CreateLiveStreamAsync(host);

        await _sut.Invoking(s => s.EndStreamAsync(stream.Id, other.Id))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*host*");
    }

    [Fact]
    public async Task EndStream_AlreadyEnded_IsIdempotent()
    {
        var host   = await CreateUserAsync();
        var stream = await CreateLiveStreamAsync(host, LiveStreamStatus.Ended);

        // Should not throw
        var act = () => _sut.EndStreamAsync(stream.Id, host.Id);

        await act.Should().NotThrowAsync();
    }

    // ── GetActiveStreamsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetActiveStreams_PublicStream_VisibleToEveryone()
    {
        var host    = await CreateUserAsync("Host");
        var viewer  = await CreateUserAsync("Viewer");
        await CreateLiveStreamAsync(host, LiveStreamStatus.Live, PrivacyLevel.Everyone);

        var streams = await _sut.GetActiveStreamsAsync(viewer.Id);

        streams.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetActiveStreams_FriendsStream_VisibleToFriend()
    {
        var host    = await CreateUserAsync("Host");
        var friend  = await CreateUserAsync("Friend");
        await MakeFriendsAsync(host.Id, friend.Id);
        await CreateLiveStreamAsync(host, LiveStreamStatus.Live, PrivacyLevel.Friends);

        var streams = await _sut.GetActiveStreamsAsync(friend.Id);

        streams.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetActiveStreams_FriendsStream_NotVisibleToStranger()
    {
        var host     = await CreateUserAsync("Host");
        var stranger = await CreateUserAsync("Stranger");
        await CreateLiveStreamAsync(host, LiveStreamStatus.Live, PrivacyLevel.Friends);

        var streams = await _sut.GetActiveStreamsAsync(stranger.Id);

        streams.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveStreams_EndedStream_NotIncluded()
    {
        var host   = await CreateUserAsync("Host");
        var viewer = await CreateUserAsync("Viewer");
        await CreateLiveStreamAsync(host, LiveStreamStatus.Ended, PrivacyLevel.Everyone);

        var streams = await _sut.GetActiveStreamsAsync(viewer.Id);

        streams.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveStreams_HostSeesOwnFriendsOnlyStream()
    {
        var host = await CreateUserAsync("Host");
        await CreateLiveStreamAsync(host, LiveStreamStatus.Live, PrivacyLevel.Friends);

        var streams = await _sut.GetActiveStreamsAsync(host.Id);

        streams.Should().HaveCount(1);
    }

    // ── GetStreamAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStream_PublicStream_VisibleToAnyone()
    {
        var host    = await CreateUserAsync("Host");
        var viewer  = await CreateUserAsync("Viewer");
        var stream  = await CreateLiveStreamAsync(host, LiveStreamStatus.Live, PrivacyLevel.Everyone);

        var dto = await _sut.GetStreamAsync(stream.Id, viewer.Id);

        dto.Id.Should().Be(stream.Id);
        dto.Title.Should().Be("Test Stream");
    }

    [Fact]
    public async Task GetStream_NotFound_ThrowsKeyNotFoundException()
    {
        var user = await CreateUserAsync();

        await _sut.Invoking(s => s.GetStreamAsync(Guid.NewGuid(), user.Id))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetStream_FriendsStream_NotVisibleToStranger()
    {
        var host     = await CreateUserAsync("Host");
        var stranger = await CreateUserAsync("Stranger");
        var stream   = await CreateLiveStreamAsync(host, LiveStreamStatus.Live, PrivacyLevel.Friends);

        await _sut.Invoking(s => s.GetStreamAsync(stream.Id, stranger.Id))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetStream_FriendsStream_VisibleToFriend()
    {
        var host   = await CreateUserAsync("Host");
        var friend = await CreateUserAsync("Friend");
        await MakeFriendsAsync(host.Id, friend.Id);
        var stream = await CreateLiveStreamAsync(host, LiveStreamStatus.Live, PrivacyLevel.Friends);

        var dto = await _sut.GetStreamAsync(stream.Id, friend.Id);

        dto.Id.Should().Be(stream.Id);
    }

    [Fact]
    public async Task GetStream_HostCanAlwaysViewOwnStream()
    {
        var host   = await CreateUserAsync("Host");
        var stream = await CreateLiveStreamAsync(host, LiveStreamStatus.Live, PrivacyLevel.Friends);

        var dto = await _sut.GetStreamAsync(stream.Id, host.Id);

        dto.Id.Should().Be(stream.Id);
    }

    [Fact]
    public async Task GetStream_EndedStream_HlsUrlIsNull()
    {
        var host   = await CreateUserAsync("Host");
        var stream = await CreateLiveStreamAsync(host, LiveStreamStatus.Ended, PrivacyLevel.Everyone);

        var dto = await _sut.GetStreamAsync(stream.Id, host.Id);

        dto.HlsUrl.Should().BeNull();
    }

    // ── GetStreamKeyAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetStreamKey_ExistingStream_ReturnsKey()
    {
        var host   = await CreateUserAsync();
        var stream = await CreateLiveStreamAsync(host);

        var key = await _sut.GetStreamKeyAsync(stream.Id);

        key.Should().Be(stream.StreamKey);
    }

    [Fact]
    public async Task GetStreamKey_NonExistentStream_ReturnsNull()
    {
        var key = await _sut.GetStreamKeyAsync(Guid.NewGuid());

        key.Should().BeNull();
    }

    // ── GetFriendIdsAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetFriendIds_ReturnsAcceptedFriends()
    {
        var alice = await CreateUserAsync("Alice");
        var bob   = await CreateUserAsync("Bob");
        var carol = await CreateUserAsync("Carol");
        await MakeFriendsAsync(alice.Id, bob.Id);
        _db.Friends.Add(new Friend
        {
            Id          = Guid.NewGuid(),
            RequesterId = alice.Id,
            AddresseeId = carol.Id,
            Status      = FriendStatus.Pending,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var ids = await _sut.GetFriendIdsAsync(alice.Id);

        ids.Should().Contain(bob.Id);
        ids.Should().NotContain(carol.Id);
    }
}
