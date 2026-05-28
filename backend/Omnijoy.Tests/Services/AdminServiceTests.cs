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
        _sut = new AdminService(_db);

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
