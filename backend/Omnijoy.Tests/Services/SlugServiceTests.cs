using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Integration-style tests against an EF Core InMemory store. Covers:
///   • CheckAsync — validation, reservation, taken (cross-table).
///   • ResolveAsync — case-insensitive, miss.
///   • SetUserSlugAsync — set, clear, idempotent re-set.
///   • SetCompanyPageSlugAsync — owner/admin allowed, editor forbidden.
///   • Cross-table uniqueness (user blocks company + vice versa).
/// </summary>
public class SlugServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly SlugService _sut;

    public SlugServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new OmnijoyDbContext(options);
        _sut = new SlugService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<User> CreateUserAsync(string displayName = "Alice", string? slug = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@test.com",
            DisplayName = displayName,
            Gender = Gender.NotDisclosed,
            UrlSlug = slug,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<CompanyPage> CreatePageAsync(
        Guid ownerId,
        string name = "Acme",
        string? slug = null,
        CompanyPageRole ownerRole = CompanyPageRole.Owner)
    {
        var page = new CompanyPage
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedByUserId = ownerId,
            UrlSlug = slug,
            CreatedAt = DateTime.UtcNow,
        };
        _db.CompanyPages.Add(page);
        _db.CompanyPageAdmins.Add(new CompanyPageAdmin
        {
            CompanyPageId = page.Id,
            UserId = ownerId,
            Role = ownerRole,
        });
        await _db.SaveChangesAsync();
        return page;
    }

    // ── CheckAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckAsync_ValidUnclaimed_ReturnsAvailable()
    {
        var result = await _sut.CheckAsync("alice");
        result.Available.Should().BeTrue();
        result.Reason.Should().BeNull();
    }

    [Fact]
    public async Task CheckAsync_Invalid_ReturnsInvalidReason()
    {
        var result = await _sut.CheckAsync("a--b");
        result.Available.Should().BeFalse();
        result.Reason.Should().Be(SlugUnavailableReason.Invalid);
    }

    [Fact]
    public async Task CheckAsync_Reserved_ReturnsReservedReason()
    {
        var result = await _sut.CheckAsync("admin");
        result.Available.Should().BeFalse();
        result.Reason.Should().Be(SlugUnavailableReason.Reserved);
    }

    [Fact]
    public async Task CheckAsync_AlreadyClaimedByUser_ReturnsTaken()
    {
        await CreateUserAsync(slug: "claimed");
        var result = await _sut.CheckAsync("claimed");
        result.Available.Should().BeFalse();
        result.Reason.Should().Be(SlugUnavailableReason.Taken);
    }

    [Fact]
    public async Task CheckAsync_AlreadyClaimedByCompanyPage_ReturnsTaken()
    {
        var owner = await CreateUserAsync();
        await CreatePageAsync(owner.Id, slug: "claimed-co");
        var result = await _sut.CheckAsync("claimed-co");
        result.Available.Should().BeFalse();
        result.Reason.Should().Be(SlugUnavailableReason.Taken);
    }

    [Fact]
    public async Task CheckAsync_UppercaseInput_ReportsInvalid()
    {
        // Slugs are stored lowercase. CheckAsync must report any input that
        // isn't already in canonical lowercase form as Invalid, so callers
        // see the same answer the (unique-lowercase) store would enforce.
        await CreateUserAsync(slug: "claimed");
        var result = await _sut.CheckAsync("CLAIMED");
        result.Available.Should().BeFalse();
        result.Reason.Should().Be(SlugUnavailableReason.Invalid);
    }

    // ── ResolveAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_UserSlug_ReturnsUserOwner()
    {
        var user = await CreateUserAsync(slug: "alice");
        var owner = await _sut.ResolveAsync("alice");

        owner.Should().NotBeNull();
        owner!.Type.Should().Be(SlugOwnerType.User);
        owner.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task ResolveAsync_CompanySlug_ReturnsCompanyOwner()
    {
        var u = await CreateUserAsync();
        var page = await CreatePageAsync(u.Id, slug: "acme");

        var owner = await _sut.ResolveAsync("acme");
        owner!.Type.Should().Be(SlugOwnerType.Company);
        owner.Id.Should().Be(page.Id);
    }

    [Fact]
    public async Task ResolveAsync_CaseInsensitive()
    {
        var user = await CreateUserAsync(slug: "alice");
        var owner = await _sut.ResolveAsync("ALICE");
        owner!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task ResolveAsync_Unknown_ReturnsNull()
    {
        var owner = await _sut.ResolveAsync("nobody-here");
        owner.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_BlankInput_ReturnsNull()
    {
        var owner = await _sut.ResolveAsync("");
        owner.Should().BeNull();
    }

    // ── SetUserSlugAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task SetUserSlugAsync_ValidSlug_PersistsLowercased()
    {
        var user = await CreateUserAsync();

        var stored = await _sut.SetUserSlugAsync(user.Id, "Alice-Doe");

        stored.Should().Be("alice-doe");
        var reloaded = await _db.Users.FindAsync(user.Id);
        reloaded!.UrlSlug.Should().Be("alice-doe");
    }

    [Fact]
    public async Task SetUserSlugAsync_NullSlug_ClearsAndFreesName()
    {
        var user = await CreateUserAsync(slug: "alice");

        var stored = await _sut.SetUserSlugAsync(user.Id, null);
        stored.Should().BeNull();

        // Slot freed — another user can now claim it.
        var other = await CreateUserAsync("Bob");
        var bobStored = await _sut.SetUserSlugAsync(other.Id, "alice");
        bobStored.Should().Be("alice");
    }

    [Fact]
    public async Task SetUserSlugAsync_BlankSlug_ClearsAsWellAsNull()
    {
        var user = await CreateUserAsync(slug: "alice");
        var stored = await _sut.SetUserSlugAsync(user.Id, "   ");
        stored.Should().BeNull();
    }

    [Fact]
    public async Task SetUserSlugAsync_ResetSameSlug_NoCollision()
    {
        // A user re-saving their own slug shouldn't false-alarm as taken.
        var user = await CreateUserAsync(slug: "alice");
        var stored = await _sut.SetUserSlugAsync(user.Id, "alice");
        stored.Should().Be("alice");
    }

    [Fact]
    public async Task SetUserSlugAsync_InvalidSlug_ThrowsInvalid()
    {
        var user = await CreateUserAsync();
        var act = () => _sut.SetUserSlugAsync(user.Id, "ab"); // too short
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("invalid");
    }

    [Fact]
    public async Task SetUserSlugAsync_ReservedSlug_ThrowsReserved()
    {
        var user = await CreateUserAsync();
        var act = () => _sut.SetUserSlugAsync(user.Id, "admin");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("reserved");
    }

    [Fact]
    public async Task SetUserSlugAsync_TakenByOtherUser_ThrowsTaken()
    {
        await CreateUserAsync("Alice", slug: "claimed");
        var bob = await CreateUserAsync("Bob");

        var act = () => _sut.SetUserSlugAsync(bob.Id, "claimed");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("taken");
    }

    [Fact]
    public async Task SetUserSlugAsync_TakenByCompany_ThrowsTaken()
    {
        // Cross-table uniqueness: company has it → user can't take it.
        var owner = await CreateUserAsync("Alice");
        await CreatePageAsync(owner.Id, slug: "acme");

        var bob = await CreateUserAsync("Bob");
        var act = () => _sut.SetUserSlugAsync(bob.Id, "acme");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("taken");
    }

    [Fact]
    public async Task SetUserSlugAsync_UnknownUser_ThrowsKeyNotFound()
    {
        var act = () => _sut.SetUserSlugAsync(Guid.NewGuid(), "alice");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── SetCompanyPageSlugAsync ───────────────────────────────────────────────

    [Fact]
    public async Task SetCompanyPageSlugAsync_Owner_Allowed()
    {
        var owner = await CreateUserAsync();
        var page = await CreatePageAsync(owner.Id);

        var stored = await _sut.SetCompanyPageSlugAsync(page.Id, owner.Id, "acme-co");
        stored.Should().Be("acme-co");
    }

    [Fact]
    public async Task SetCompanyPageSlugAsync_Admin_Allowed()
    {
        var creator = await CreateUserAsync("creator");
        var admin = await CreateUserAsync("admin");
        var page = await CreatePageAsync(creator.Id);
        _db.CompanyPageAdmins.Add(new CompanyPageAdmin
        {
            CompanyPageId = page.Id,
            UserId = admin.Id,
            Role = CompanyPageRole.Admin,
        });
        await _db.SaveChangesAsync();

        var stored = await _sut.SetCompanyPageSlugAsync(page.Id, admin.Id, "acme-by-admin");
        stored.Should().Be("acme-by-admin");
    }

    [Fact]
    public async Task SetCompanyPageSlugAsync_Editor_Forbidden()
    {
        var creator = await CreateUserAsync("creator");
        var editor = await CreateUserAsync("editor");
        var page = await CreatePageAsync(creator.Id);
        _db.CompanyPageAdmins.Add(new CompanyPageAdmin
        {
            CompanyPageId = page.Id,
            UserId = editor.Id,
            Role = CompanyPageRole.Editor,
        });
        await _db.SaveChangesAsync();

        var act = () => _sut.SetCompanyPageSlugAsync(page.Id, editor.Id, "shouldfail");
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task SetCompanyPageSlugAsync_Outsider_Forbidden()
    {
        var creator = await CreateUserAsync("creator");
        var outsider = await CreateUserAsync("outsider");
        var page = await CreatePageAsync(creator.Id);

        var act = () => _sut.SetCompanyPageSlugAsync(page.Id, outsider.Id, "shouldfail");
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task SetCompanyPageSlugAsync_NullSlug_ClearsAndFreesName()
    {
        var owner = await CreateUserAsync();
        var page = await CreatePageAsync(owner.Id, slug: "acme");

        var stored = await _sut.SetCompanyPageSlugAsync(page.Id, owner.Id, null);
        stored.Should().BeNull();

        // Slot freed — a user can now claim it.
        var newUser = await CreateUserAsync("Bob");
        var taken = await _sut.SetUserSlugAsync(newUser.Id, "acme");
        taken.Should().Be("acme");
    }

    [Fact]
    public async Task SetCompanyPageSlugAsync_TakenByUser_ThrowsTaken()
    {
        // Cross-table uniqueness from the company side.
        await CreateUserAsync("Alice", slug: "claimed");

        var owner = await CreateUserAsync("OwnerOfPage");
        var page = await CreatePageAsync(owner.Id);

        var act = () => _sut.SetCompanyPageSlugAsync(page.Id, owner.Id, "claimed");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("taken");
    }

    [Fact]
    public async Task SetCompanyPageSlugAsync_TakenByOtherCompany_ThrowsTaken()
    {
        var owner = await CreateUserAsync();
        await CreatePageAsync(owner.Id, name: "A", slug: "shared");
        var pageB = await CreatePageAsync(owner.Id, name: "B");

        var act = () => _sut.SetCompanyPageSlugAsync(pageB.Id, owner.Id, "shared");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("taken");
    }

    [Fact]
    public async Task SetCompanyPageSlugAsync_ResetSameSlug_NoCollision()
    {
        var owner = await CreateUserAsync();
        var page = await CreatePageAsync(owner.Id, slug: "acme");
        var stored = await _sut.SetCompanyPageSlugAsync(page.Id, owner.Id, "acme");
        stored.Should().Be("acme");
    }

    [Fact]
    public async Task SetCompanyPageSlugAsync_UnknownPage_ThrowsKeyNotFound()
    {
        var u = await CreateUserAsync();
        var act = () => _sut.SetCompanyPageSlugAsync(Guid.NewGuid(), u.Id, "acme");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SetCompanyPageSlugAsync_InvalidSlug_ThrowsInvalid()
    {
        var owner = await CreateUserAsync();
        var page = await CreatePageAsync(owner.Id);
        var act = () => _sut.SetCompanyPageSlugAsync(page.Id, owner.Id, "ab");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("invalid");
    }

    [Fact]
    public async Task SetCompanyPageSlugAsync_ReservedSlug_ThrowsReserved()
    {
        var owner = await CreateUserAsync();
        var page = await CreatePageAsync(owner.Id);
        var act = () => _sut.SetCompanyPageSlugAsync(page.Id, owner.Id, "Settings");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("reserved");
    }
}
