using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Omnijoy.Core.DTOs.CompanyPages;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

public class CompanyPageServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly Mock<IMediaStorageService> _storageMock;
    private readonly CompanyPageService _sut;

    public CompanyPageServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new OmnijoyDbContext(options);
        _storageMock = new Mock<IMediaStorageService>();
        _sut = new CompanyPageService(_db, _storageMock.Object);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<User> CreateUserAsync(string name = "User")
    {
        var u = new User
        {
            Id          = Guid.NewGuid(),
            Email       = $"{Guid.NewGuid()}@test.com",
            DisplayName = name,
            Gender      = Gender.NotDisclosed,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        };
        _db.Users.Add(u);
        await _db.SaveChangesAsync();
        return u;
    }

    private async Task<CompanyPageDto> CreatePageAsync(Guid creatorId, string name = "Acme Corp")
    {
        return await _sut.CreatePageAsync(creatorId, new CreateCompanyPageRequest(name, "A company"), null, null);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePage_CreatorBecomesOwner()
    {
        var creator = await CreateUserAsync("Creator");

        var dto = await CreatePageAsync(creator.Id);

        dto.Should().NotBeNull();
        dto.Name.Should().Be("Acme Corp");
        dto.MyRole.Should().Be("Owner");
        dto.FollowerCount.Should().Be(0);

        var admin = await _db.CompanyPageAdmins.FirstAsync();
        admin.UserId.Should().Be(creator.Id);
        admin.Role.Should().Be(CompanyPageRole.Owner);
    }

    [Fact]
    public async Task CreatePage_EmptyName_ThrowsArgumentException()
    {
        var creator = await CreateUserAsync();

        await _sut.Invoking(s => s.CreatePageAsync(creator.Id,
            new CreateCompanyPageRequest("   ", null), null, null))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Name*");
    }

    [Fact]
    public async Task CreatePage_WithLogo_UploadsAndSetsLogoUrl()
    {
        var creator = await CreateUserAsync();
        _storageMock
            .Setup(s => s.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), "company/logos"))
            .ReturnsAsync("/uploads/company/logos/logo.png");

        var logo = new PageImageUploadItem(new MemoryStream([0x89, 0x50, 0x4E, 0x47]), "logo.png", "image/png", "logo");

        var dto = await _sut.CreatePageAsync(creator.Id, new CreateCompanyPageRequest("Corp", null), logo, null);

        dto.LogoUrl.Should().Be("/uploads/company/logos/logo.png");
    }

    // ── GetPage ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPage_ExistingPage_ReturnsDto()
    {
        var creator = await CreateUserAsync("Creator");
        var created = await CreatePageAsync(creator.Id);

        var dto = await _sut.GetPageAsync(created.Id, creator.Id);

        dto.Name.Should().Be("Acme Corp");
        dto.MyRole.Should().Be("Owner");
    }

    [Fact]
    public async Task GetPage_NotFound_ThrowsKeyNotFound()
    {
        await _sut.Invoking(s => s.GetPageAsync(Guid.NewGuid(), null))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── UpdatePage ────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePage_OwnerCanEdit()
    {
        var creator = await CreateUserAsync("Creator");
        var created = await CreatePageAsync(creator.Id);

        var updated = await _sut.UpdatePageAsync(created.Id, creator.Id,
            new UpdateCompanyPageRequest("New Name", "New desc"), null, null);

        updated.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task UpdatePage_AdminCanEdit()
    {
        var owner = await CreateUserAsync("Owner");
        var admin = await CreateUserAsync("Admin");
        var created = await CreatePageAsync(owner.Id);

        // Promote admin user to Admin role
        await _sut.AddOrUpdateAdminAsync(created.Id, owner.Id, new AddAdminRequest(admin.Id, "Admin"));

        var updated = await _sut.UpdatePageAsync(created.Id, admin.Id,
            new UpdateCompanyPageRequest("Name by Admin", null), null, null);

        updated.Name.Should().Be("Name by Admin");
    }

    [Fact]
    public async Task UpdatePage_EditorCannotEdit_ThrowsUnauthorized()
    {
        var owner  = await CreateUserAsync("Owner");
        var editor = await CreateUserAsync("Editor");
        var created = await CreatePageAsync(owner.Id);

        await _sut.AddOrUpdateAdminAsync(created.Id, owner.Id, new AddAdminRequest(editor.Id, "Editor"));

        await _sut.Invoking(s => s.UpdatePageAsync(created.Id, editor.Id,
            new UpdateCompanyPageRequest("Hack", null), null, null))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Owners and Admins*");
    }

    [Fact]
    public async Task UpdatePage_NonAdmin_ThrowsUnauthorized()
    {
        var owner    = await CreateUserAsync("Owner");
        var stranger = await CreateUserAsync("Stranger");
        var created  = await CreatePageAsync(owner.Id);

        await _sut.Invoking(s => s.UpdatePageAsync(created.Id, stranger.Id,
            new UpdateCompanyPageRequest("Hack", null), null, null))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── Admins ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAdmin_OwnerCanAddAdmin()
    {
        var owner    = await CreateUserAsync("Owner");
        var newAdmin = await CreateUserAsync("NewAdmin");
        var created  = await CreatePageAsync(owner.Id);

        var result = await _sut.AddOrUpdateAdminAsync(created.Id, owner.Id, new AddAdminRequest(newAdmin.Id, "Admin"));

        result.Admins.Should().HaveCount(2);
        result.Admins.Should().Contain(a => a.UserId == newAdmin.Id && a.Role == "Admin");
    }

    [Fact]
    public async Task AddAdmin_EditorCannotManageAdmins_ThrowsUnauthorized()
    {
        var owner  = await CreateUserAsync("Owner");
        var editor = await CreateUserAsync("Editor");
        var other  = await CreateUserAsync("Other");
        var created = await CreatePageAsync(owner.Id);
        await _sut.AddOrUpdateAdminAsync(created.Id, owner.Id, new AddAdminRequest(editor.Id, "Editor"));

        await _sut.Invoking(s => s.AddOrUpdateAdminAsync(created.Id, editor.Id, new AddAdminRequest(other.Id, "Editor")))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Editors cannot*");
    }

    [Fact]
    public async Task RemoveAdmin_CannotRemoveLastOwner_ThrowsInvalidOp()
    {
        var owner   = await CreateUserAsync("Owner");
        var created = await CreatePageAsync(owner.Id);

        await _sut.Invoking(s => s.RemoveAdminAsync(created.Id, owner.Id, owner.Id))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*last Owner*");
    }

    [Fact]
    public async Task RemoveAdmin_AdminCanRemoveEditor()
    {
        var owner  = await CreateUserAsync("Owner");
        var admin  = await CreateUserAsync("Admin");
        var editor = await CreateUserAsync("Editor");
        var created = await CreatePageAsync(owner.Id);
        await _sut.AddOrUpdateAdminAsync(created.Id, owner.Id, new AddAdminRequest(admin.Id, "Admin"));
        await _sut.AddOrUpdateAdminAsync(created.Id, owner.Id, new AddAdminRequest(editor.Id, "Editor"));

        var result = await _sut.RemoveAdminAsync(created.Id, admin.Id, editor.Id);

        result.Admins.Should().NotContain(a => a.UserId == editor.Id);
    }

    // ── Follow / Unfollow ─────────────────────────────────────────────────────

    [Fact]
    public async Task Follow_NonFollower_CreatesFollowRecord()
    {
        var creator  = await CreateUserAsync("Creator");
        var follower = await CreateUserAsync("Follower");
        var created  = await CreatePageAsync(creator.Id);

        var dto = await _sut.FollowAsync(created.Id, follower.Id);

        dto.FollowerCount.Should().Be(1);
        dto.IsFollowing.Should().BeTrue();
        (await _db.CompanyPageFollows.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Follow_AlreadyFollowing_IsIdempotent()
    {
        var creator  = await CreateUserAsync("Creator");
        var follower = await CreateUserAsync("Follower");
        var created  = await CreatePageAsync(creator.Id);

        await _sut.FollowAsync(created.Id, follower.Id);
        await _sut.FollowAsync(created.Id, follower.Id); // second call

        (await _db.CompanyPageFollows.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Unfollow_Following_RemovesRecord()
    {
        var creator  = await CreateUserAsync("Creator");
        var follower = await CreateUserAsync("Follower");
        var created  = await CreatePageAsync(creator.Id);
        await _sut.FollowAsync(created.Id, follower.Id);

        var dto = await _sut.UnfollowAsync(created.Id, follower.Id);

        dto.FollowerCount.Should().Be(0);
        dto.IsFollowing.Should().BeFalse();
        (await _db.CompanyPageFollows.CountAsync()).Should().Be(0);
    }

    // ── GetPages ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPages_MineOnly_ReturnsOnlyUserAdminPages()
    {
        var owner    = await CreateUserAsync("Owner");
        var otherOwner = await CreateUserAsync("OtherOwner");

        await CreatePageAsync(owner.Id, "My Page");
        await CreatePageAsync(otherOwner.Id, "Other Page");

        var result = await _sut.GetPagesAsync(owner.Id, mineOnly: true, 1, 20);

        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("My Page");
    }

    [Fact]
    public async Task GetMyAdminPages_ReturnsAdminPages()
    {
        var owner = await CreateUserAsync("Owner");
        await CreatePageAsync(owner.Id, "Page1");
        await CreatePageAsync(owner.Id, "Page2");

        var pages = await _sut.GetMyAdminPagesAsync(owner.Id);

        pages.Should().HaveCount(2);
    }
}
