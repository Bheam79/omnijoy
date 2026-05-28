using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Tests for <see cref="ModerationLogService"/> — append-only audit-log writer
/// and paginated reader.
/// </summary>
public class ModerationLogServiceTests : IDisposable
{
    private readonly OmnijoyDbContext      _db;
    private readonly ModerationLogService  _sut;

    public ModerationLogServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db  = new OmnijoyDbContext(options);
        _sut = new ModerationLogService(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<User> CreateUserAsync(string display = "Admin", UserRole role = UserRole.Admin)
    {
        var user = new User
        {
            Id          = Guid.NewGuid(),
            Email       = $"{Guid.NewGuid()}@t.com",
            DisplayName = display,
            Gender      = Gender.NotDisclosed,
            Role        = role,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    // ── LogAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task LogAsync_WritesRowAndReturnsId()
    {
        var actor = await CreateUserAsync();
        var targetId = Guid.NewGuid().ToString();

        var id = await _sut.LogAsync(
            actor.Id, ModerationAction.BanUser, "User", targetId, "spam");

        id.Should().NotBeEmpty();

        var row = await _db.ModerationLogs.SingleAsync();
        row.Id.Should().Be(id);
        row.ActorId.Should().Be(actor.Id);
        row.Action.Should().Be(ModerationAction.BanUser);
        row.TargetType.Should().Be("User");
        row.TargetId.Should().Be(targetId);
        row.Notes.Should().Be("spam");
        row.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task LogAsync_NullNotes_StoresNull()
    {
        var actor = await CreateUserAsync();

        await _sut.LogAsync(actor.Id, ModerationAction.RemovePost, "Post",
            Guid.NewGuid().ToString(), null);

        var row = await _db.ModerationLogs.SingleAsync();
        row.Notes.Should().BeNull();
    }

    [Fact]
    public async Task LogAsync_WhitespaceNotes_StoresNull()
    {
        var actor = await CreateUserAsync();

        await _sut.LogAsync(actor.Id, ModerationAction.RemovePost, "Post",
            Guid.NewGuid().ToString(), "   ");

        var row = await _db.ModerationLogs.SingleAsync();
        row.Notes.Should().BeNull();
    }

    [Fact]
    public async Task LogAsync_EmptyTargetType_ThrowsArgumentException()
    {
        var actor = await CreateUserAsync();

        await _sut.Invoking(s => s.LogAsync(
                actor.Id, ModerationAction.BanUser, "", "x", null))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task LogAsync_EmptyTargetId_ThrowsArgumentException()
    {
        var actor = await CreateUserAsync();

        await _sut.Invoking(s => s.LogAsync(
                actor.Id, ModerationAction.BanUser, "User", "", null))
            .Should().ThrowAsync<ArgumentException>();
    }

    // ── ListAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_NoFilter_ReturnsAllNewestFirst()
    {
        var actor = await CreateUserAsync();
        var t1 = Guid.NewGuid().ToString();
        var t2 = Guid.NewGuid().ToString();

        await _sut.LogAsync(actor.Id, ModerationAction.BanUser, "User", t1, null);
        await Task.Delay(10);
        await _sut.LogAsync(actor.Id, ModerationAction.UnbanUser, "User", t2, null);

        var result = await _sut.ListAsync(null, null, 1, 20);

        result.Items.Should().HaveCount(2);
        result.Items[0].Action.Should().Be("UnbanUser");
        result.Items[1].Action.Should().Be("BanUser");
        result.HasMore.Should().BeFalse();
        result.Page.Should().Be(1);
    }

    [Fact]
    public async Task ListAsync_FilterByActorId_ReturnsOnlyThatActor()
    {
        var alice = await CreateUserAsync("Alice");
        var bob   = await CreateUserAsync("Bob");

        await _sut.LogAsync(alice.Id, ModerationAction.BanUser, "User", Guid.NewGuid().ToString(), null);
        await _sut.LogAsync(bob.Id,   ModerationAction.BanUser, "User", Guid.NewGuid().ToString(), null);
        await _sut.LogAsync(alice.Id, ModerationAction.UnbanUser, "User", Guid.NewGuid().ToString(), null);

        var result = await _sut.ListAsync(alice.Id, null, 1, 20);

        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(i => i.ActorId == alice.Id);
        result.Items.Should().OnlyContain(i => i.ActorDisplayName == "Alice");
    }

    [Fact]
    public async Task ListAsync_FilterByAction_ReturnsOnlyMatchingAction()
    {
        var actor = await CreateUserAsync();

        await _sut.LogAsync(actor.Id, ModerationAction.BanUser, "User", Guid.NewGuid().ToString(), null);
        await _sut.LogAsync(actor.Id, ModerationAction.UnbanUser, "User", Guid.NewGuid().ToString(), null);
        await _sut.LogAsync(actor.Id, ModerationAction.BanUser, "User", Guid.NewGuid().ToString(), null);

        var bans = await _sut.ListAsync(null, "BanUser", 1, 20);
        var unbans = await _sut.ListAsync(null, "UnbanUser", 1, 20);

        bans.Items.Should().HaveCount(2);
        bans.Items.Should().OnlyContain(i => i.Action == "BanUser");
        unbans.Items.Should().HaveCount(1);
        unbans.Items.Should().OnlyContain(i => i.Action == "UnbanUser");
    }

    [Fact]
    public async Task ListAsync_UnknownActionString_IgnoresFilter()
    {
        var actor = await CreateUserAsync();

        await _sut.LogAsync(actor.Id, ModerationAction.BanUser, "User", Guid.NewGuid().ToString(), null);

        // "BogusAction" doesn't parse so the filter is dropped, returning everything
        var result = await _sut.ListAsync(null, "BogusAction", 1, 20);

        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListAsync_Pagination_HasMoreIsCorrect()
    {
        var actor = await CreateUserAsync();

        for (int i = 0; i < 5; i++)
        {
            await _sut.LogAsync(actor.Id, ModerationAction.BanUser, "User",
                Guid.NewGuid().ToString(), null);
        }

        var page1 = await _sut.ListAsync(null, null, 1, 2);
        var page2 = await _sut.ListAsync(null, null, 2, 2);
        var page3 = await _sut.ListAsync(null, null, 3, 2);

        page1.Items.Should().HaveCount(2);
        page1.HasMore.Should().BeTrue();

        page2.Items.Should().HaveCount(2);
        page2.HasMore.Should().BeTrue();

        page3.Items.Should().HaveCount(1);
        page3.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task ListAsync_PageSizeClampedToMax100()
    {
        var actor = await CreateUserAsync();
        await _sut.LogAsync(actor.Id, ModerationAction.BanUser, "User",
            Guid.NewGuid().ToString(), null);

        var result = await _sut.ListAsync(null, null, 1, 500);

        result.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task ListAsync_PageBelow1_NormalizedToPage1()
    {
        var actor = await CreateUserAsync();
        await _sut.LogAsync(actor.Id, ModerationAction.BanUser, "User",
            Guid.NewGuid().ToString(), null);

        var result = await _sut.ListAsync(null, null, 0, 20);

        result.Page.Should().Be(1);
        result.Items.Should().HaveCount(1);
    }
}
