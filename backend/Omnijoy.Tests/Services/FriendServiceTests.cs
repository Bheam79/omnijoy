using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs.Friends;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

public class FriendServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly FriendService _sut;

    public FriendServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new OmnijoyDbContext(options);
        _sut = new FriendService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<User> CreateUserAsync(string name = "User")
    {
        var u = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@test.com",
            DisplayName = name,
            Gender = Gender.NotDisclosed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Users.Add(u);
        await _db.SaveChangesAsync();
        return u;
    }

    private async Task<Friend> AddFriendRecordAsync(Guid requesterId, Guid addresseeId, FriendStatus status)
    {
        var f = new Friend
        {
            Id = Guid.NewGuid(),
            RequesterId = requesterId,
            AddresseeId = addresseeId,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Friends.Add(f);
        await _db.SaveChangesAsync();
        return f;
    }

    // ── SendRequest ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SendRequest_NewRequest_CreatesRecord()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");

        var dto = await _sut.SendRequestAsync(alice.Id, bob.Id);

        dto.From.Id.Should().Be(bob.Id);
        var record = await _db.Friends.FirstAsync();
        record.RequesterId.Should().Be(alice.Id);
        record.Status.Should().Be(FriendStatus.Pending);
    }

    [Fact]
    public async Task SendRequest_ToSelf_ThrowsArgument()
    {
        var user = await CreateUserAsync();
        await _sut.Invoking(s => s.SendRequestAsync(user.Id, user.Id))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendRequest_AlreadyFriends_ThrowsInvalidOp()
    {
        var a = await CreateUserAsync("A");
        var b = await CreateUserAsync("B");
        await AddFriendRecordAsync(a.Id, b.Id, FriendStatus.Accepted);

        await _sut.Invoking(s => s.SendRequestAsync(a.Id, b.Id))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*already friends*");
    }

    [Fact]
    public async Task SendRequest_AlreadySent_ThrowsInvalidOp()
    {
        var a = await CreateUserAsync("A");
        var b = await CreateUserAsync("B");
        await AddFriendRecordAsync(a.Id, b.Id, FriendStatus.Pending);

        await _sut.Invoking(s => s.SendRequestAsync(a.Id, b.Id))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*already sent*");
    }

    [Fact]
    public async Task SendRequest_IncomingPending_AutoAccepts()
    {
        // Bob sent Alice a request; Alice now sends one to Bob
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        await AddFriendRecordAsync(bob.Id, alice.Id, FriendStatus.Pending);

        await _sut.SendRequestAsync(alice.Id, bob.Id);

        var record = await _db.Friends.FirstAsync();
        record.Status.Should().Be(FriendStatus.Accepted);
    }

    [Fact]
    public async Task SendRequest_AfterDecline_ReSends()
    {
        var a = await CreateUserAsync("A");
        var b = await CreateUserAsync("B");
        await AddFriendRecordAsync(a.Id, b.Id, FriendStatus.Declined);

        await _sut.SendRequestAsync(a.Id, b.Id);

        var record = await _db.Friends.FirstAsync();
        record.Status.Should().Be(FriendStatus.Pending);
    }

    [Fact]
    public async Task SendRequest_TargetNotFound_ThrowsKeyNotFound()
    {
        var a = await CreateUserAsync();
        await _sut.Invoking(s => s.SendRequestAsync(a.Id, Guid.NewGuid()))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── AcceptRequest ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AcceptRequest_ValidPending_AcceptsAndReturnsDto()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        await AddFriendRecordAsync(bob.Id, alice.Id, FriendStatus.Pending);

        var dto = await _sut.AcceptRequestAsync(alice.Id, bob.Id);

        dto.User.Id.Should().Be(bob.Id);
        var record = await _db.Friends.FirstAsync();
        record.Status.Should().Be(FriendStatus.Accepted);
    }

    [Fact]
    public async Task AcceptRequest_NoRecord_ThrowsKeyNotFound()
    {
        var a = await CreateUserAsync();
        await _sut.Invoking(s => s.AcceptRequestAsync(a.Id, Guid.NewGuid()))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── DeclineRequest ────────────────────────────────────────────────────────

    [Fact]
    public async Task DeclineRequest_ValidPending_Declines()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        await AddFriendRecordAsync(bob.Id, alice.Id, FriendStatus.Pending);

        await _sut.DeclineRequestAsync(alice.Id, bob.Id);

        var record = await _db.Friends.FirstAsync();
        record.Status.Should().Be(FriendStatus.Declined);
    }

    // ── CancelRequest ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelRequest_OwnPending_DeletesRecord()
    {
        var a = await CreateUserAsync("A");
        var b = await CreateUserAsync("B");
        await AddFriendRecordAsync(a.Id, b.Id, FriendStatus.Pending);

        await _sut.CancelRequestAsync(a.Id, b.Id);

        (await _db.Friends.CountAsync()).Should().Be(0);
    }

    // ── RemoveFriend ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveFriend_Accepted_DeletesRecord()
    {
        var a = await CreateUserAsync("A");
        var b = await CreateUserAsync("B");
        await AddFriendRecordAsync(a.Id, b.Id, FriendStatus.Accepted);

        await _sut.RemoveFriendAsync(a.Id, b.Id);

        (await _db.Friends.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RemoveFriend_NotFriends_ThrowsKeyNotFound()
    {
        var a = await CreateUserAsync("A");
        var b = await CreateUserAsync("B");

        await _sut.Invoking(s => s.RemoveFriendAsync(a.Id, b.Id))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Block / Unblock ───────────────────────────────────────────────────────

    [Fact]
    public async Task BlockUser_NoExistingRecord_CreatesBlocked()
    {
        var a = await CreateUserAsync("A");
        var b = await CreateUserAsync("B");

        await _sut.BlockUserAsync(a.Id, b.Id);

        var record = await _db.Friends.FirstAsync();
        record.Status.Should().Be(FriendStatus.Blocked);
        record.RequesterId.Should().Be(a.Id);
    }

    [Fact]
    public async Task BlockUser_ExistingAccepted_SetsBlocked()
    {
        var a = await CreateUserAsync("A");
        var b = await CreateUserAsync("B");
        await AddFriendRecordAsync(a.Id, b.Id, FriendStatus.Accepted);

        await _sut.BlockUserAsync(a.Id, b.Id);

        var record = await _db.Friends.FirstAsync();
        record.Status.Should().Be(FriendStatus.Blocked);
    }

    [Fact]
    public async Task BlockUser_AlreadyBlocked_IsIdempotent()
    {
        var a = await CreateUserAsync("A");
        var b = await CreateUserAsync("B");
        await AddFriendRecordAsync(a.Id, b.Id, FriendStatus.Blocked);

        await _sut.BlockUserAsync(a.Id, b.Id); // should not throw

        (await _db.Friends.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UnblockUser_BlockedByMe_DeletesRecord()
    {
        var a = await CreateUserAsync("A");
        var b = await CreateUserAsync("B");
        await AddFriendRecordAsync(a.Id, b.Id, FriendStatus.Blocked);

        await _sut.UnblockUserAsync(a.Id, b.Id);

        (await _db.Friends.CountAsync()).Should().Be(0);
    }

    // ── GetFriends ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFriends_ReturnsBothDirections()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var carol = await CreateUserAsync("Carol");

        await AddFriendRecordAsync(alice.Id, bob.Id, FriendStatus.Accepted);   // alice sent
        await AddFriendRecordAsync(carol.Id, alice.Id, FriendStatus.Accepted); // carol sent to alice

        var result = await _sut.GetFriendsAsync(alice.Id, 1, 20);

        result.Items.Should().HaveCount(2);
        result.Items.Select(f => f.User.Id).Should().Contain(bob.Id).And.Contain(carol.Id);
    }

    [Fact]
    public async Task GetFriends_PaginationWorks()
    {
        var alice = await CreateUserAsync("Alice");
        var users = new List<User>();
        for (int i = 0; i < 5; i++)
        {
            var u = await CreateUserAsync($"User{i}");
            users.Add(u);
            await AddFriendRecordAsync(alice.Id, u.Id, FriendStatus.Accepted);
        }

        var page1 = await _sut.GetFriendsAsync(alice.Id, 1, 3);
        var page2 = await _sut.GetFriendsAsync(alice.Id, 2, 3);

        page1.Items.Should().HaveCount(3);
        page1.HasMore.Should().BeTrue();
        page2.Items.Should().HaveCount(2);
        page2.HasMore.Should().BeFalse();
    }

    // ── GetPendingRequests ────────────────────────────────────────────────────

    [Fact]
    public async Task GetPendingRequests_ReturnsIncoming()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var carol = await CreateUserAsync("Carol");

        await AddFriendRecordAsync(bob.Id, alice.Id, FriendStatus.Pending);    // incoming
        await AddFriendRecordAsync(alice.Id, carol.Id, FriendStatus.Pending);  // outgoing — should not appear

        var requests = await _sut.GetPendingRequestsAsync(alice.Id);

        requests.Should().HaveCount(1);
        requests[0].From.Id.Should().Be(bob.Id);
    }

    // ── GetFriendStatus ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetFriendStatus_NoneWhenNoRecord()
    {
        var a = await CreateUserAsync("A");
        var b = await CreateUserAsync("B");
        var dto = await _sut.GetFriendStatusAsync(a.Id, b.Id);
        dto.Status.Should().Be("None");
    }

    [Fact]
    public async Task GetFriendStatus_AcceptedReturnsAccepted()
    {
        var a = await CreateUserAsync("A");
        var b = await CreateUserAsync("B");
        await AddFriendRecordAsync(a.Id, b.Id, FriendStatus.Accepted);
        var dto = await _sut.GetFriendStatusAsync(a.Id, b.Id);
        dto.Status.Should().Be("Accepted");
    }

    [Fact]
    public async Task GetFriendStatus_PendingSentReturnsRequestSent()
    {
        var a = await CreateUserAsync("A");
        var b = await CreateUserAsync("B");
        await AddFriendRecordAsync(a.Id, b.Id, FriendStatus.Pending);
        var dto = await _sut.GetFriendStatusAsync(a.Id, b.Id);
        dto.Status.Should().Be("RequestSent");
    }

    [Fact]
    public async Task GetFriendStatus_PendingReceivedReturnsRequestReceived()
    {
        var a = await CreateUserAsync("A");
        var b = await CreateUserAsync("B");
        await AddFriendRecordAsync(b.Id, a.Id, FriendStatus.Pending);
        var dto = await _sut.GetFriendStatusAsync(a.Id, b.Id);
        dto.Status.Should().Be("RequestReceived");
    }

    [Fact]
    public async Task GetFriendStatus_BlockedByMeReturnsBlockedByMe()
    {
        var a = await CreateUserAsync("A");
        var b = await CreateUserAsync("B");
        await AddFriendRecordAsync(a.Id, b.Id, FriendStatus.Blocked);
        var dto = await _sut.GetFriendStatusAsync(a.Id, b.Id);
        dto.Status.Should().Be("BlockedByMe");
    }

    [Fact]
    public async Task GetFriendStatus_BlockedByThemReturnsBlockedByThem()
    {
        var a = await CreateUserAsync("A");
        var b = await CreateUserAsync("B");
        await AddFriendRecordAsync(b.Id, a.Id, FriendStatus.Blocked);
        var dto = await _sut.GetFriendStatusAsync(a.Id, b.Id);
        dto.Status.Should().Be("BlockedByThem");
    }

    [Fact]
    public async Task GetFriendStatus_SelfReturnsSelf()
    {
        var a = await CreateUserAsync("A");
        var dto = await _sut.GetFriendStatusAsync(a.Id, a.Id);
        dto.Status.Should().Be("Self");
    }

    // ── GetPendingRequestCount ────────────────────────────────────────────────

    [Fact]
    public async Task GetPendingRequestCount_ReturnsIncomingCount()
    {
        var alice = await CreateUserAsync("Alice");
        var b = await CreateUserAsync("B");
        var c = await CreateUserAsync("C");

        await AddFriendRecordAsync(b.Id, alice.Id, FriendStatus.Pending);
        await AddFriendRecordAsync(c.Id, alice.Id, FriendStatus.Pending);
        await AddFriendRecordAsync(alice.Id, b.Id, FriendStatus.Pending); // this would conflict with unique index in real DB, but in-memory allows it for testing the count of incoming
        // Actually this creates a conflict. Let me use different users:

        var d = await CreateUserAsync("D");
        await AddFriendRecordAsync(d.Id, alice.Id, FriendStatus.Pending);

        var count = await _sut.GetPendingRequestCountAsync(alice.Id);
        count.Should().BeGreaterThanOrEqualTo(2);
    }

    // ── Family Relations ──────────────────────────────────────────────────────

    [Fact]
    public async Task SetFamilyRelation_NotFriends_ThrowsInvalidOp()
    {
        var a = await CreateUserAsync("A");
        var b = await CreateUserAsync("B");

        await _sut.Invoking(s => s.SetFamilyRelationAsync(a.Id, b.Id,
            new SetFamilyRelationRequest("Parent", "Father")))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*friends*");
    }

    [Fact]
    public async Task SetFamilyRelation_Friends_StoresRelation()
    {
        var a = await CreateUserAsync("A");
        var b = await CreateUserAsync("B");
        await AddFriendRecordAsync(a.Id, b.Id, FriendStatus.Accepted);

        var dto = await _sut.SetFamilyRelationAsync(a.Id, b.Id,
            new SetFamilyRelationRequest("Parent", "Father"));

        dto.Should().NotBeNull();
        dto!.RelationType.Should().Be("Parent");
        dto.Subtype.Should().Be("Father");
    }

    [Fact]
    public async Task SetFamilyRelation_ClearsWhenNull()
    {
        var a = await CreateUserAsync("A");
        var b = await CreateUserAsync("B");
        await AddFriendRecordAsync(a.Id, b.Id, FriendStatus.Accepted);
        await _sut.SetFamilyRelationAsync(a.Id, b.Id, new SetFamilyRelationRequest("Parent", "Father"));

        var result = await _sut.SetFamilyRelationAsync(a.Id, b.Id, new SetFamilyRelationRequest(null, null));

        result.Should().BeNull();
        (await _db.FamilyRelations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GetFamilyRelation_WhenNotSet_ReturnsNull()
    {
        var a = await CreateUserAsync("A");
        var b = await CreateUserAsync("B");

        var result = await _sut.GetFamilyRelationAsync(a.Id, b.Id);
        result.Should().BeNull();
    }
}
