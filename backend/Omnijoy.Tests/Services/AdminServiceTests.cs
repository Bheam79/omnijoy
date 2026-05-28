using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Tests for:
/// <list type="bullet">
///   <item><see cref="AdminService.ChangeUserRoleAsync"/> — role change business logic</item>
///   <item><see cref="TokenService.GenerateAccessToken"/> — correct role claim emission</item>
/// </list>
/// </summary>
public class AdminServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly AdminService     _sut;
    private readonly TokenService     _tokens;

    public AdminServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db  = new OmnijoyDbContext(options);
        _sut = new AdminService(_db, new ModerationLogService(_db));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"]                    = "test-secret-key-that-is-at-least-32-chars!!",
                ["Jwt:Issuer"]                 = "omnijoy-test",
                ["Jwt:Audience"]               = "omnijoy-test",
                ["Jwt:AccessTokenExpiryMinutes"] = "60",
            })
            .Build();
        _tokens = new TokenService(config);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<User> CreateUserAsync(UserRole role = UserRole.User, string email = "user@test.com")
    {
        var user = new User
        {
            Id          = Guid.NewGuid(),
            Email       = email,
            DisplayName = "Test",
            Gender      = Gender.NotDisclosed,
            Role        = role,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    // ── TokenService: role claim emission ────────────────────────────────────

    [Fact]
    public void GenerateAccessToken_UserRole_EmitsNoRoleClaim()
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "u@t.com", DisplayName = "U",
            Gender = Gender.NotDisclosed, Role = UserRole.User,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };

        var tokenString = _tokens.GenerateAccessToken(user);
        var claims      = ParseClaims(tokenString);

        claims.Should().NotContain(c => c.Type == ClaimTypes.Role);
    }

    [Fact]
    public void GenerateAccessToken_ModeratorRole_EmitsModeratorRoleClaim()
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "m@t.com", DisplayName = "M",
            Gender = Gender.NotDisclosed, Role = UserRole.Moderator,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };

        var tokenString = _tokens.GenerateAccessToken(user);
        var claims      = ParseClaims(tokenString);

        var roleClaim = claims.FirstOrDefault(c =>
            c.Type == ClaimTypes.Role ||
            c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
        roleClaim.Should().NotBeNull();
        roleClaim!.Value.Should().Be("Moderator");
    }

    [Fact]
    public void GenerateAccessToken_AdminRole_EmitsAdminRoleClaim()
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "a@t.com", DisplayName = "A",
            Gender = Gender.NotDisclosed, Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };

        var tokenString = _tokens.GenerateAccessToken(user);
        var claims      = ParseClaims(tokenString);

        var roleClaim = claims.FirstOrDefault(c =>
            c.Type == ClaimTypes.Role ||
            c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
        roleClaim.Should().NotBeNull();
        roleClaim!.Value.Should().Be("Admin");
    }

    // ── AdminService.ChangeUserRoleAsync ─────────────────────────────────────

    [Fact]
    public async Task ChangeUserRole_ValidRole_UpdatesRoleAndReturnsDto()
    {
        var admin  = await CreateUserAsync(UserRole.Admin, "admin@t.com");
        var target = await CreateUserAsync(UserRole.User,  "target@t.com");

        var result = await _sut.ChangeUserRoleAsync(admin.Id, target.Id, "Moderator");

        result.Should().NotBeNull();
        result.Id.Should().Be(target.Id);
        result.Role.Should().Be("Moderator");

        var updated = await _db.Users.FindAsync(target.Id);
        updated!.Role.Should().Be(UserRole.Moderator);
    }

    [Fact]
    public async Task ChangeUserRole_AdminToAdmin_Idempotent()
    {
        var admin  = await CreateUserAsync(UserRole.Admin, "admin@t.com");
        var target = await CreateUserAsync(UserRole.Admin, "target@t.com");

        var result = await _sut.ChangeUserRoleAsync(admin.Id, target.Id, "Admin");

        result.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task ChangeUserRole_Demote_AdminToUser_Succeeds()
    {
        var admin  = await CreateUserAsync(UserRole.Admin, "admin@t.com");
        var target = await CreateUserAsync(UserRole.Admin, "other@t.com");

        var result = await _sut.ChangeUserRoleAsync(admin.Id, target.Id, "User");

        result.Role.Should().Be("User");
        var updated = await _db.Users.FindAsync(target.Id);
        updated!.Role.Should().Be(UserRole.User);
    }

    [Fact]
    public async Task ChangeUserRole_InvalidRole_ThrowsArgumentException()
    {
        var admin  = await CreateUserAsync(UserRole.Admin, "admin@t.com");
        var target = await CreateUserAsync(UserRole.User,  "target@t.com");

        await _sut.Invoking(s => s.ChangeUserRoleAsync(admin.Id, target.Id, "SuperAdmin"))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*SuperAdmin*");
    }

    [Fact]
    public async Task ChangeUserRole_UnknownTarget_ThrowsKeyNotFound()
    {
        var admin = await CreateUserAsync(UserRole.Admin, "admin@t.com");

        await _sut.Invoking(s => s.ChangeUserRoleAsync(admin.Id, Guid.NewGuid(), "Moderator"))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Theory]
    [InlineData("user",      "User")]
    [InlineData("moderator", "Moderator")]
    [InlineData("admin",     "Admin")]
    public async Task ChangeUserRole_RoleIsCaseInsensitive(string input, string expected)
    {
        var admin  = await CreateUserAsync(UserRole.Admin, "admin@t.com");
        var target = await CreateUserAsync(UserRole.User,  $"target_{input}@t.com");

        var result = await _sut.ChangeUserRoleAsync(admin.Id, target.Id, input);

        result.Role.Should().Be(expected);
    }

    // ── AdminService.BanUserAsync / UnbanUserAsync ───────────────────────────

    [Fact]
    public async Task BanUser_FlipsIsBannedAndStampsBannedAt()
    {
        var admin  = await CreateUserAsync(UserRole.Admin,     "admin@t.com");
        var target = await CreateUserAsync(UserRole.User,      "target@t.com");

        var result = await _sut.BanUserAsync(admin.Id, target.Id, "spammy");

        result.IsBanned.Should().BeTrue();
        result.BannedAt.Should().NotBeNull();

        var refreshed = await _db.Users.FindAsync(target.Id);
        refreshed!.IsBanned.Should().BeTrue();
        refreshed.BannedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task BanUser_WritesModerationLogEntry()
    {
        var admin  = await CreateUserAsync(UserRole.Admin, "admin@t.com");
        var target = await CreateUserAsync(UserRole.User,  "target@t.com");

        await _sut.BanUserAsync(admin.Id, target.Id, "TOS violation");

        var log = _db.ModerationLogs.Single();
        log.ActorId.Should().Be(admin.Id);
        log.Action.Should().Be(ModerationAction.BanUser);
        log.TargetType.Should().Be("User");
        log.TargetId.Should().Be(target.Id.ToString());
        log.Notes.Should().Be("TOS violation");
    }

    [Fact]
    public async Task BanUser_UnknownTarget_ThrowsKeyNotFound()
    {
        var admin = await CreateUserAsync(UserRole.Admin, "admin@t.com");

        await _sut.Invoking(s => s.BanUserAsync(admin.Id, Guid.NewGuid(), null))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task BanUser_AlreadyBanned_IsIdempotentButStillLogs()
    {
        var admin  = await CreateUserAsync(UserRole.Admin, "admin@t.com");
        var target = await CreateUserAsync(UserRole.User,  "target@t.com");

        await _sut.BanUserAsync(admin.Id, target.Id, "first");
        await _sut.BanUserAsync(admin.Id, target.Id, "second");

        var refreshed = await _db.Users.FindAsync(target.Id);
        refreshed!.IsBanned.Should().BeTrue();

        _db.ModerationLogs.Count().Should().Be(2);
    }

    [Fact]
    public async Task UnbanUser_ClearsIsBannedAndBannedAt()
    {
        var admin  = await CreateUserAsync(UserRole.Admin, "admin@t.com");
        var target = await CreateUserAsync(UserRole.User,  "target@t.com");

        await _sut.BanUserAsync(admin.Id, target.Id, null);
        var result = await _sut.UnbanUserAsync(admin.Id, target.Id, "appealed");

        result.IsBanned.Should().BeFalse();
        result.BannedAt.Should().BeNull();

        var refreshed = await _db.Users.FindAsync(target.Id);
        refreshed!.IsBanned.Should().BeFalse();
        refreshed.BannedAt.Should().BeNull();
    }

    [Fact]
    public async Task UnbanUser_WritesModerationLogEntry()
    {
        var admin  = await CreateUserAsync(UserRole.Admin, "admin@t.com");
        var target = await CreateUserAsync(UserRole.User,  "target@t.com");

        await _sut.BanUserAsync(admin.Id, target.Id, null);
        await _sut.UnbanUserAsync(admin.Id, target.Id, "mistake");

        // 2 entries: BanUser + UnbanUser
        _db.ModerationLogs.Count().Should().Be(2);
        var unban = _db.ModerationLogs
            .Single(l => l.Action == ModerationAction.UnbanUser);
        unban.ActorId.Should().Be(admin.Id);
        unban.TargetId.Should().Be(target.Id.ToString());
        unban.Notes.Should().Be("mistake");
    }

    [Fact]
    public async Task UnbanUser_UnknownTarget_ThrowsKeyNotFound()
    {
        var admin = await CreateUserAsync(UserRole.Admin, "admin@t.com");

        await _sut.Invoking(s => s.UnbanUserAsync(admin.Id, Guid.NewGuid(), null))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ChangeUserRole_WritesModerationLogEntry()
    {
        var admin  = await CreateUserAsync(UserRole.Admin, "admin@t.com");
        var target = await CreateUserAsync(UserRole.User,  "target@t.com");

        await _sut.ChangeUserRoleAsync(admin.Id, target.Id, "Moderator");

        var log = _db.ModerationLogs.Single();
        log.Action.Should().Be(ModerationAction.ChangeRole);
        log.ActorId.Should().Be(admin.Id);
        log.TargetType.Should().Be("User");
        log.TargetId.Should().Be(target.Id.ToString());
        log.Notes.Should().Contain("User").And.Contain("Moderator");
    }

    // ── AdminService.ListUsersAsync ───────────────────────────────────────────

    [Fact]
    public async Task ListUsers_NoFilter_ReturnsAllPaginated()
    {
        await CreateUserAsync(UserRole.User, "alice@t.com");
        await CreateUserAsync(UserRole.User, "bob@t.com");
        await CreateUserAsync(UserRole.User, "carol@t.com");

        var result = await _sut.ListUsersAsync(null, 1, 2);

        result.Items.Should().HaveCount(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
        result.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task ListUsers_QueryMatchesEmailOrDisplayName()
    {
        var alice = await CreateUserAsync(UserRole.User, "alice@t.com");
        alice.DisplayName = "Alice Wonder";
        await _db.SaveChangesAsync();
        await CreateUserAsync(UserRole.User, "bob@t.com");

        var byEmail = await _sut.ListUsersAsync("alice", 1, 20);
        var byName  = await _sut.ListUsersAsync("Wonder", 1, 20);

        byEmail.Items.Should().HaveCount(1);
        byEmail.Items[0].Email.Should().Be("alice@t.com");
        byName.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListUsers_PageSizeClampedToMax100()
    {
        await CreateUserAsync(UserRole.User, "a@t.com");

        var result = await _sut.ListUsersAsync(null, 1, 500);

        result.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task ListUsers_Page0NormalizedToPage1()
    {
        await CreateUserAsync(UserRole.User, "a@t.com");

        var result = await _sut.ListUsersAsync(null, 0, 20);

        result.Page.Should().Be(1);
    }

    [Fact]
    public async Task ListUsers_DtoExposesIsBannedFlag()
    {
        var admin  = await CreateUserAsync(UserRole.Admin, "admin@t.com");
        var target = await CreateUserAsync(UserRole.User,  "tgt@t.com");
        await _sut.BanUserAsync(admin.Id, target.Id, null);

        var result = await _sut.ListUsersAsync("tgt", 1, 20);

        result.Items.Should().HaveCount(1);
        result.Items[0].IsBanned.Should().BeTrue();
        result.Items[0].BannedAt.Should().NotBeNull();
    }

    // ── UserDto includes role ─────────────────────────────────────────────────

    [Theory]
    [InlineData(UserRole.User,      "User")]
    [InlineData(UserRole.Moderator, "Moderator")]
    [InlineData(UserRole.Admin,     "Admin")]
    public void UserService_MapUserDto_IncludesRoleString(UserRole role, string expectedStr)
    {
        var user = new User
        {
            Id          = Guid.NewGuid(),
            Email       = "dto@t.com",
            DisplayName = "Dto",
            Gender      = Gender.NotDisclosed,
            Role        = role,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        };

        var dto = UserService.MapUserDto(user);

        dto.Role.Should().Be(expectedStr);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses the JWT (without signature validation) and returns all claims.
    /// </summary>
    private static IReadOnlyList<Claim> ParseClaims(string tokenString)
    {
        var handler = new JwtSecurityTokenHandler();
        var token   = handler.ReadJwtToken(tokenString);
        return token.Claims.ToList();
    }
}
