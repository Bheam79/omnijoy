using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Core.Interfaces;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

public class FriendInviteServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly Mock<IEmailService> _email;
    private readonly FriendInviteService _sut;

    private const string BaseUrl = "https://omnijoy.test";

    public FriendInviteServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db    = new OmnijoyDbContext(options);
        _email = new Mock<IEmailService>();
        _sut   = new FriendInviteService(_db, _email.Object);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<User> CreateUserAsync(string? email = null, string name = "User")
    {
        var u = new User
        {
            Id          = Guid.NewGuid(),
            Email       = email ?? $"{Guid.NewGuid()}@test.com",
            DisplayName = name,
            Gender      = Gender.NotDisclosed,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        };
        _db.Users.Add(u);
        await _db.SaveChangesAsync();
        return u;
    }

    private async Task<FriendInvite> CreateInviteAsync(
        Guid inviterId,
        string? email        = null,
        FriendInviteStatus status = FriendInviteStatus.Pending,
        DateTime? expiresAt  = null)
    {
        var now = DateTime.UtcNow;
        var inv = new FriendInvite
        {
            Id        = Guid.NewGuid(),
            InviterId = inviterId,
            Token     = Guid.NewGuid().ToString("N"),
            Email     = email,
            Status    = status,
            ExpiresAt = expiresAt ?? now.AddDays(30),
            CreatedAt = now,
        };
        _db.FriendInvites.Add(inv);
        await _db.SaveChangesAsync();
        return inv;
    }

    private async Task<Friend> AddFriendAsync(Guid requesterId, Guid addresseeId, FriendStatus status)
    {
        var f = new Friend
        {
            Id          = Guid.NewGuid(),
            RequesterId = requesterId,
            AddresseeId = addresseeId,
            Status      = status,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        };
        _db.Friends.Add(f);
        await _db.SaveChangesAsync();
        return f;
    }

    // ── CreateInviteLinkAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateInviteLinkAsync_NoExisting_CreatesNewInvite()
    {
        var inviter = await CreateUserAsync();

        var result = await _sut.CreateInviteLinkAsync(inviter.Id, BaseUrl);

        result.Token.Should().NotBeNullOrEmpty();
        result.InviteUrl.Should().StartWith(BaseUrl).And.Contain(result.Token);
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        (await _db.FriendInvites.CountAsync(fi => fi.InviterId == inviter.Id))
            .Should().Be(1);
    }

    [Fact]
    public async Task CreateInviteLinkAsync_ValidPendingLinkExists_ReusesIt()
    {
        var inviter = await CreateUserAsync();
        var existing = await CreateInviteAsync(inviter.Id);

        var result = await _sut.CreateInviteLinkAsync(inviter.Id, BaseUrl);

        result.Token.Should().Be(existing.Token);

        (await _db.FriendInvites.CountAsync(fi => fi.InviterId == inviter.Id))
            .Should().Be(1, "should reuse the existing invite, not create a duplicate");
    }

    [Fact]
    public async Task CreateInviteLinkAsync_ExpiredLinkInvite_CreatesNew()
    {
        var inviter = await CreateUserAsync();
        await CreateInviteAsync(inviter.Id, expiresAt: DateTime.UtcNow.AddSeconds(-1));

        var result = await _sut.CreateInviteLinkAsync(inviter.Id, BaseUrl);

        (await _db.FriendInvites.CountAsync(fi => fi.InviterId == inviter.Id))
            .Should().Be(2);
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task CreateInviteLinkAsync_RevokedLinkInvite_CreatesNew()
    {
        var inviter = await CreateUserAsync();
        await CreateInviteAsync(inviter.Id, status: FriendInviteStatus.Revoked);

        var result = await _sut.CreateInviteLinkAsync(inviter.Id, BaseUrl);

        result.Token.Should().NotBeNullOrEmpty();
        (await _db.FriendInvites.CountAsync(fi => fi.InviterId == inviter.Id))
            .Should().Be(2);
    }

    [Fact]
    public async Task CreateInviteLinkAsync_ExistingEmailInvite_CreatesNewLinkInvite()
    {
        var inviter = await CreateUserAsync();
        await CreateInviteAsync(inviter.Id, email: "someone@ext.com");

        await _sut.CreateInviteLinkAsync(inviter.Id, BaseUrl);

        (await _db.FriendInvites.CountAsync(fi => fi.InviterId == inviter.Id))
            .Should().Be(2, "email invites are not reused for link invites");
    }

    [Fact]
    public async Task CreateInviteLinkAsync_ExpiresAt_IsApprox30DaysFromNow()
    {
        var inviter = await CreateUserAsync();

        var result = await _sut.CreateInviteLinkAsync(inviter.Id, BaseUrl);

        result.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromMinutes(1));
    }

    // ── InviteByEmailAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task InviteByEmailAsync_InviterNotFound_Throws()
    {
        var act = () => _sut.InviteByEmailAsync(Guid.NewGuid(), "a@b.com", BaseUrl);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task InviteByEmailAsync_SelfInvite_Throws()
    {
        var inviter = await CreateUserAsync(email: "alice@test.com");

        var act = () => _sut.InviteByEmailAsync(inviter.Id, "alice@test.com", BaseUrl);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task InviteByEmailAsync_SelfInvite_CaseInsensitive()
    {
        var inviter = await CreateUserAsync(email: "Alice@Test.com");

        var act = () => _sut.InviteByEmailAsync(inviter.Id, "ALICE@TEST.COM", BaseUrl);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task InviteByEmailAsync_TargetAlreadyExists_AutoAcceptsAndReturnsAccepted()
    {
        var inviter = await CreateUserAsync(name: "Inviter");
        var target  = await CreateUserAsync(email: "target@test.com", name: "Target");

        var result = await _sut.InviteByEmailAsync(inviter.Id, "target@test.com", BaseUrl);

        result.Outcome.Should().Be("accepted");
        result.DisplayName.Should().Be(target.DisplayName);

        (await _db.Friends.AnyAsync(f =>
            (f.RequesterId == inviter.Id && f.AddresseeId == target.Id) ||
            (f.RequesterId == target.Id  && f.AddresseeId == inviter.Id)))
            .Should().BeTrue();

        _email.Verify(
            e => e.SendFriendInviteEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task InviteByEmailAsync_NewEmail_SendsEmailAndReturnsInvited()
    {
        var inviter = await CreateUserAsync(name: "Inviter");

        var result = await _sut.InviteByEmailAsync(inviter.Id, "new@external.com", BaseUrl);

        result.Outcome.Should().Be("invited");
        result.DisplayName.Should().BeNull();

        _email.Verify(
            e => e.SendFriendInviteEmailAsync("new@external.com", "Inviter", It.IsAny<string>()),
            Times.Once);

        (await _db.FriendInvites.CountAsync(fi =>
            fi.InviterId == inviter.Id && fi.Email == "new@external.com"))
            .Should().Be(1);
    }

    [Fact]
    public async Task InviteByEmailAsync_DuplicatePendingEmailInvite_SkipsSend()
    {
        var inviter = await CreateUserAsync();
        await CreateInviteAsync(inviter.Id, email: "existing@external.com");

        var result = await _sut.InviteByEmailAsync(inviter.Id, "existing@external.com", BaseUrl);

        result.Outcome.Should().Be("invited");

        _email.Verify(
            e => e.SendFriendInviteEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);

        (await _db.FriendInvites.CountAsync(fi =>
            fi.InviterId == inviter.Id && fi.Email == "existing@external.com"))
            .Should().Be(1, "should not create a duplicate invite row");
    }

    [Fact]
    public async Task InviteByEmailAsync_TargetAlreadyFriends_NoOpAndReturnsAccepted()
    {
        var inviter = await CreateUserAsync();
        var target  = await CreateUserAsync(email: "buddy@test.com");
        await AddFriendAsync(inviter.Id, target.Id, FriendStatus.Accepted);

        var result = await _sut.InviteByEmailAsync(inviter.Id, "buddy@test.com", BaseUrl);

        result.Outcome.Should().Be("accepted");

        (await _db.Friends.CountAsync(f =>
            (f.RequesterId == inviter.Id && f.AddresseeId == target.Id) ||
            (f.RequesterId == target.Id  && f.AddresseeId == inviter.Id)))
            .Should().Be(1, "no duplicate friend record should be created");
    }

    [Fact]
    public async Task InviteByEmailAsync_TargetBlocked_ReturnsAcceptedWithoutChangingBlock()
    {
        var inviter = await CreateUserAsync();
        var target  = await CreateUserAsync(email: "blocked@test.com");
        await AddFriendAsync(inviter.Id, target.Id, FriendStatus.Blocked);

        var result = await _sut.InviteByEmailAsync(inviter.Id, "blocked@test.com", BaseUrl);

        result.Outcome.Should().Be("accepted");

        var record = await _db.Friends.SingleAsync(f =>
            f.RequesterId == inviter.Id && f.AddresseeId == target.Id);
        record.Status.Should().Be(FriendStatus.Blocked, "blocks must not be overwritten");
    }

    // ── GetInviteInfoAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetInviteInfoAsync_ValidToken_ReturnsInviterInfo()
    {
        var inviter = await CreateUserAsync(name: "Inviter", email: "inviter@test.com");
        var invite  = await CreateInviteAsync(inviter.Id);

        var result = await _sut.GetInviteInfoAsync(invite.Token);

        result.InviterDisplayName.Should().Be("Inviter");
        result.InviterId.Should().Be(inviter.Id);
    }

    [Fact]
    public async Task GetInviteInfoAsync_UnknownToken_Throws()
    {
        var act = () => _sut.GetInviteInfoAsync("nonexistent-token");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── AcceptInviteAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task AcceptInviteAsync_HappyPath_CreatesFriendshipAndMarksAccepted()
    {
        var inviter  = await CreateUserAsync(name: "Inviter");
        var acceptor = await CreateUserAsync();
        var invite   = await CreateInviteAsync(inviter.Id);

        var result = await _sut.AcceptInviteAsync(invite.Token, acceptor.Id);

        result.InviterDisplayName.Should().Be("Inviter");
        result.InviterId.Should().Be(inviter.Id);

        var row = await _db.FriendInvites.SingleAsync(fi => fi.Id == invite.Id);
        row.Status.Should().Be(FriendInviteStatus.Accepted);
        row.AcceptedByUserId.Should().Be(acceptor.Id);
        row.AcceptedAt.Should().NotBeNull();

        (await _db.Friends.AnyAsync(f =>
            (f.RequesterId == inviter.Id  && f.AddresseeId == acceptor.Id) ||
            (f.RequesterId == acceptor.Id && f.AddresseeId == inviter.Id)))
            .Should().BeTrue();
    }

    [Fact]
    public async Task AcceptInviteAsync_UnknownToken_Throws()
    {
        var act = () => _sut.AcceptInviteAsync("bad-token", Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task AcceptInviteAsync_SelfAccept_Throws()
    {
        var inviter = await CreateUserAsync();
        var invite  = await CreateInviteAsync(inviter.Id);

        var act = () => _sut.AcceptInviteAsync(invite.Token, inviter.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*own invite*");
    }

    [Fact]
    public async Task AcceptInviteAsync_RevokedInvite_Throws()
    {
        var inviter  = await CreateUserAsync();
        var acceptor = await CreateUserAsync();
        var invite   = await CreateInviteAsync(inviter.Id, status: FriendInviteStatus.Revoked);

        var act = () => _sut.AcceptInviteAsync(invite.Token, acceptor.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*revoked*");
    }

    [Fact]
    public async Task AcceptInviteAsync_ExpiredInvite_Throws()
    {
        var inviter  = await CreateUserAsync();
        var acceptor = await CreateUserAsync();
        var invite   = await CreateInviteAsync(inviter.Id, expiresAt: DateTime.UtcNow.AddSeconds(-1));

        var act = () => _sut.AcceptInviteAsync(invite.Token, acceptor.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expired*");
    }

    [Fact]
    public async Task AcceptInviteAsync_AlreadyAccepted_IsIdempotent()
    {
        var inviter  = await CreateUserAsync(name: "Inviter");
        var acceptor = await CreateUserAsync();
        var invite   = await CreateInviteAsync(inviter.Id, status: FriendInviteStatus.Accepted);

        var result = await _sut.AcceptInviteAsync(invite.Token, acceptor.Id);

        result.InviterDisplayName.Should().Be("Inviter");

        (await _db.Friends.CountAsync()).Should().Be(0,
            "idempotent path should not create a duplicate friend row");
    }

    [Fact]
    public async Task AcceptInviteAsync_ExpiredButAlreadyAccepted_IsIdempotent()
    {
        var inviter  = await CreateUserAsync(name: "Inviter");
        var acceptor = await CreateUserAsync();
        var invite   = await CreateInviteAsync(
            inviter.Id,
            status: FriendInviteStatus.Accepted,
            expiresAt: DateTime.UtcNow.AddSeconds(-1));

        var act = () => _sut.AcceptInviteAsync(invite.Token, acceptor.Id);

        await act.Should().NotThrowAsync();

        (await _db.Friends.CountAsync()).Should().Be(0,
            "an already-accepted invite must stay idempotent even after expiring");
    }

    [Fact]
    public async Task AcceptInviteAsync_ExistingPendingFriend_UpgradesToAccepted()
    {
        var inviter  = await CreateUserAsync();
        var acceptor = await CreateUserAsync();
        var invite   = await CreateInviteAsync(inviter.Id);
        await AddFriendAsync(inviter.Id, acceptor.Id, FriendStatus.Pending);

        await _sut.AcceptInviteAsync(invite.Token, acceptor.Id);

        var row = await _db.Friends.SingleAsync();
        row.Status.Should().Be(FriendStatus.Accepted);
    }

    [Fact]
    public async Task AcceptInviteAsync_ExistingBlock_RespectsBlock()
    {
        var inviter  = await CreateUserAsync();
        var acceptor = await CreateUserAsync();
        var invite   = await CreateInviteAsync(inviter.Id);
        await AddFriendAsync(inviter.Id, acceptor.Id, FriendStatus.Blocked);

        await _sut.AcceptInviteAsync(invite.Token, acceptor.Id);

        var row = await _db.Friends.SingleAsync();
        row.Status.Should().Be(FriendStatus.Blocked, "blocks must not be overwritten by invite acceptance");
    }

    // ── RevokeLinkInvitesAsync ────────────────────────────────────────────────

    [Fact]
    public async Task RevokeLinkInvitesAsync_HasPendingLinks_RevokesAll()
    {
        var inviter = await CreateUserAsync();
        await CreateInviteAsync(inviter.Id);
        await CreateInviteAsync(inviter.Id);

        await _sut.RevokeLinkInvitesAsync(inviter.Id);

        var rows = await _db.FriendInvites
            .Where(fi => fi.InviterId == inviter.Id)
            .ToListAsync();

        rows.Should().AllSatisfy(fi => fi.Status.Should().Be(FriendInviteStatus.Revoked));
    }

    [Fact]
    public async Task RevokeLinkInvitesAsync_NoPendingLinks_NoOp()
    {
        var inviter = await CreateUserAsync();

        var act = () => _sut.RevokeLinkInvitesAsync(inviter.Id);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RevokeLinkInvitesAsync_DoesNotRevokeEmailInvites()
    {
        var inviter = await CreateUserAsync();
        await CreateInviteAsync(inviter.Id, email: "someone@ext.com");

        await _sut.RevokeLinkInvitesAsync(inviter.Id);

        var emailInvite = await _db.FriendInvites.SingleAsync(fi => fi.Email != null);
        emailInvite.Status.Should().Be(FriendInviteStatus.Pending,
            "RevokeLinkInvites should only affect link-based (null-email) invites");
    }

    [Fact]
    public async Task RevokeLinkInvitesAsync_AlreadyRevokedInvites_Unaffected()
    {
        var inviter = await CreateUserAsync();
        await CreateInviteAsync(inviter.Id, status: FriendInviteStatus.Revoked);

        await _sut.RevokeLinkInvitesAsync(inviter.Id);

        (await _db.FriendInvites.CountAsync(fi => fi.Status == FriendInviteStatus.Revoked))
            .Should().Be(1);
    }
}
