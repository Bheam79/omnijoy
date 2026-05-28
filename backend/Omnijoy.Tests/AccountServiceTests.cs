using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Omnijoy.Core.DTOs.Notifications;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests;

public class AccountServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly AccountService _sut;

    private const string ValidPassword = "P@ssw0rd!";

    public AccountServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new OmnijoyDbContext(options);
        _sut = new AccountService(_db);
    }

    private AccountService MakeSutWithConfig(IDictionary<string, string?> values)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return new AccountService(_db, config);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<User> CreateUserAsync(string email = "user@example.com", string? password = ValidPassword)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = "Test User",
            PasswordHash = password is not null ? BCrypt.Net.BCrypt.HashPassword(password) : null,
            Gender = Gender.NotDisclosed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        user.PrivacySettings = new UserPrivacySettings { UserId = user.Id };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    // ── ChangeEmailAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task ChangeEmail_ValidRequest_UpdatesEmail()
    {
        var user = await CreateUserAsync();

        await _sut.ChangeEmailAsync(user.Id, "new@example.com", ValidPassword);

        var updated = await _db.Users.FindAsync(user.Id);
        updated!.Email.Should().Be("new@example.com");
    }

    [Fact]
    public async Task ChangeEmail_NormalizesToLowercase()
    {
        var user = await CreateUserAsync();

        await _sut.ChangeEmailAsync(user.Id, "New@EXAMPLE.COM", ValidPassword);

        var updated = await _db.Users.FindAsync(user.Id);
        updated!.Email.Should().Be("new@example.com");
    }

    [Fact]
    public async Task ChangeEmail_WrongPassword_ThrowsUnauthorized()
    {
        var user = await CreateUserAsync();

        var act = async () => await _sut.ChangeEmailAsync(user.Id, "new@example.com", "WrongPassword!");

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*password*");
    }

    [Fact]
    public async Task ChangeEmail_DuplicateEmail_ThrowsInvalidOperation()
    {
        var user1 = await CreateUserAsync("first@example.com");
        await CreateUserAsync("second@example.com");

        var act = async () => await _sut.ChangeEmailAsync(user1.Id, "second@example.com", ValidPassword);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task ChangeEmail_InvalidEmailFormat_ThrowsArgumentException()
    {
        var user = await CreateUserAsync();

        var act = async () => await _sut.ChangeEmailAsync(user.Id, "not-an-email", ValidPassword);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid email format*");
    }

    [Fact]
    public async Task ChangeEmail_EmptyEmail_ThrowsArgumentException()
    {
        var user = await CreateUserAsync();

        var act = async () => await _sut.ChangeEmailAsync(user.Id, "  ", ValidPassword);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*required*");
    }

    [Fact]
    public async Task ChangeEmail_NoPasswordHash_ThrowsUnauthorized()
    {
        var user = await CreateUserAsync(password: null);

        var act = async () => await _sut.ChangeEmailAsync(user.Id, "new@example.com", ValidPassword);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ChangeEmail_UserNotFound_ThrowsKeyNotFound()
    {
        var act = async () => await _sut.ChangeEmailAsync(Guid.NewGuid(), "new@example.com", ValidPassword);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── ChangePasswordAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_ValidRequest_UpdatesPasswordHash()
    {
        var user = await CreateUserAsync();
        var oldHash = user.PasswordHash;

        await _sut.ChangePasswordAsync(user.Id, ValidPassword, "NewP@ssw0rd!", "NewP@ssw0rd!");

        var updated = await _db.Users.FindAsync(user.Id);
        updated!.PasswordHash.Should().NotBe(oldHash);
        BCrypt.Net.BCrypt.Verify("NewP@ssw0rd!", updated.PasswordHash!).Should().BeTrue();
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_ThrowsUnauthorized()
    {
        var user = await CreateUserAsync();

        var act = async () => await _sut.ChangePasswordAsync(user.Id, "WrongCurrent!", "NewP@ssw0rd!", "NewP@ssw0rd!");

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*password*");
    }

    [Fact]
    public async Task ChangePassword_MismatchedConfirmation_ThrowsArgumentException()
    {
        var user = await CreateUserAsync();

        var act = async () => await _sut.ChangePasswordAsync(user.Id, ValidPassword, "NewP@ssw0rd!", "DifferentP@ss!");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*do not match*");
    }

    [Fact]
    public async Task ChangePassword_TooShort_ThrowsArgumentException()
    {
        var user = await CreateUserAsync();

        var act = async () => await _sut.ChangePasswordAsync(user.Id, ValidPassword, "Short1!", "Short1!");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*8 characters*");
    }

    [Fact]
    public async Task ChangePassword_EmptyNewPassword_ThrowsArgumentException()
    {
        var user = await CreateUserAsync();

        var act = async () => await _sut.ChangePasswordAsync(user.Id, ValidPassword, "  ", "  ");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*required*");
    }

    [Fact]
    public async Task ChangePassword_NoPasswordHash_ThrowsUnauthorized()
    {
        var user = await CreateUserAsync(password: null);

        var act = async () => await _sut.ChangePasswordAsync(user.Id, ValidPassword, "NewP@ssw0rd!", "NewP@ssw0rd!");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ChangePassword_UserNotFound_ThrowsKeyNotFound()
    {
        var act = async () => await _sut.ChangePasswordAsync(Guid.NewGuid(), ValidPassword, "NewP@ssw0rd!", "NewP@ssw0rd!");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── GetNotificationPreferencesAsync ──────────────────────────────────────

    [Fact]
    public async Task GetNotificationPreferences_NoExistingRow_CreatesDefaults()
    {
        var user = await CreateUserAsync();

        var prefs = await _sut.GetNotificationPreferencesAsync(user.Id);

        prefs.LikesOnMyPosts.Should().BeTrue();
        prefs.CommentsOnMyPosts.Should().BeTrue();
        prefs.FriendRequests.Should().BeTrue();
        prefs.EventInvites.Should().BeTrue();
        prefs.NewFollower.Should().BeTrue();

        // Persisted to DB.
        (await _db.NotificationPreferences.CountAsync(p => p.UserId == user.Id))
            .Should().Be(1);
    }

    [Fact]
    public async Task GetNotificationPreferences_ExistingRow_ReturnsStoredValues()
    {
        var user = await CreateUserAsync();
        _db.NotificationPreferences.Add(new NotificationPreferences
        {
            UserId            = user.Id,
            LikesOnMyPosts    = false,
            EventInvites      = false,
        });
        await _db.SaveChangesAsync();

        var prefs = await _sut.GetNotificationPreferencesAsync(user.Id);

        prefs.LikesOnMyPosts.Should().BeFalse();
        prefs.EventInvites.Should().BeFalse();
        prefs.CommentsOnMyPosts.Should().BeTrue();
    }

    [Fact]
    public async Task GetNotificationPreferences_UserNotFound_ThrowsKeyNotFound()
    {
        var act = async () => await _sut.GetNotificationPreferencesAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── UpdateNotificationPreferencesAsync ───────────────────────────────────

    [Fact]
    public async Task UpdateNotificationPreferences_PersistsAndReturnsValues()
    {
        var user = await CreateUserAsync();

        var dto = new NotificationPreferencesDto(
            LikesOnMyPosts:      false,
            CommentsOnMyPosts:   true,
            PostShares:          false,
            FriendRequests:      false,
            NewFollower:         true,
            Mentions:            false,
            NewPostsFromFriends: true,
            FamilyRelations:     false,
            EventInvites:        true,
            DirectMessages:      false,
            LiveStreams:         true,
            CompanyPageInvites:  false);

        var result = await _sut.UpdateNotificationPreferencesAsync(user.Id, dto);

        result.Should().Be(dto);

        var stored = await _db.NotificationPreferences.FirstAsync(p => p.UserId == user.Id);
        stored.LikesOnMyPosts.Should().BeFalse();
        stored.CommentsOnMyPosts.Should().BeTrue();
        stored.Mentions.Should().BeFalse();
        stored.CompanyPageInvites.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateNotificationPreferences_NoExistingRow_CreatesRow()
    {
        var user = await CreateUserAsync();

        var dto = new NotificationPreferencesDto(
            LikesOnMyPosts: false, CommentsOnMyPosts: false, PostShares: false,
            FriendRequests: false, NewFollower: false, Mentions: false,
            NewPostsFromFriends: false, FamilyRelations: false,
            EventInvites: false, DirectMessages: false, LiveStreams: false,
            CompanyPageInvites: false);

        await _sut.UpdateNotificationPreferencesAsync(user.Id, dto);

        (await _db.NotificationPreferences.CountAsync(p => p.UserId == user.Id))
            .Should().Be(1);
    }

    [Fact]
    public async Task UpdateNotificationPreferences_UserNotFound_ThrowsKeyNotFound()
    {
        var dto = new NotificationPreferencesDto(
            true, true, true, true, true, true, true, true, true, true, true, true);

        var act = async () => await _sut.UpdateNotificationPreferencesAsync(Guid.NewGuid(), dto);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── DeactivateAccountAsync ────────────────────────────────────────────────

    [Fact]
    public async Task DeactivateAccount_MarksUserInactive_AndStampsDeactivatedAt()
    {
        var user = await CreateUserAsync();

        await _sut.DeactivateAccountAsync(user.Id);

        var updated = await _db.Users.FindAsync(user.Id);
        updated!.IsActive.Should().BeFalse();
        updated.DeactivatedAt.Should().NotBeNull();
        updated.DeactivatedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DeactivateAccount_RevokesAllOutstandingRefreshTokens()
    {
        var user = await CreateUserAsync();
        _db.RefreshTokens.AddRange(
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = "hash-a",
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false,
            },
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = "hash-b",
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false,
            });
        await _db.SaveChangesAsync();

        await _sut.DeactivateAccountAsync(user.Id);

        (await _db.RefreshTokens.Where(rt => rt.UserId == user.Id).ToListAsync())
            .Should().OnlyContain(rt => rt.IsRevoked);
    }

    [Fact]
    public async Task DeactivateAccount_UserNotFound_ThrowsKeyNotFound()
    {
        var act = async () => await _sut.DeactivateAccountAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeactivateAccount_AlreadyPendingDeletion_ThrowsInvalidOperation()
    {
        var user = await CreateUserAsync();
        user.DeletionScheduledAt = DateTime.UtcNow;
        user.IsActive = false;
        await _db.SaveChangesAsync();

        var act = async () => await _sut.DeactivateAccountAsync(user.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*pending deletion*");
    }

    // ── DeleteAccountAsync (soft delete path) ────────────────────────────────

    [Fact]
    public async Task DeleteAccount_SoftDelete_MarksDeletionScheduledAndInactive()
    {
        var user = await CreateUserAsync();

        await _sut.DeleteAccountAsync(user.Id, user.Email);

        var updated = await _db.Users.FindAsync(user.Id);
        updated.Should().NotBeNull();
        updated!.IsActive.Should().BeFalse();
        updated.DeletionScheduledAt.Should().NotBeNull();
        updated.DeactivatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAccount_SoftDelete_RevokesAllOutstandingRefreshTokens()
    {
        var user = await CreateUserAsync();
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = "h",
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false,
        });
        await _db.SaveChangesAsync();

        await _sut.DeleteAccountAsync(user.Id, user.Email);

        (await _db.RefreshTokens.Where(rt => rt.UserId == user.Id).ToListAsync())
            .Should().OnlyContain(rt => rt.IsRevoked);
    }

    [Fact]
    public async Task DeleteAccount_EmailConfirmationCaseInsensitive_Succeeds()
    {
        var user = await CreateUserAsync(email: "alice@example.com");

        await _sut.DeleteAccountAsync(user.Id, "ALICE@example.com");

        var updated = await _db.Users.FindAsync(user.Id);
        updated!.DeletionScheduledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAccount_EmptyConfirmation_ThrowsArgument()
    {
        var user = await CreateUserAsync();

        var act = async () => await _sut.DeleteAccountAsync(user.Id, "  ");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*required*");
    }

    [Fact]
    public async Task DeleteAccount_MismatchedEmail_ThrowsArgument()
    {
        var user = await CreateUserAsync(email: "alice@example.com");

        var act = async () => await _sut.DeleteAccountAsync(user.Id, "bob@example.com");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*does not match*");
    }

    [Fact]
    public async Task DeleteAccount_UserNotFound_ThrowsKeyNotFound()
    {
        var act = async () => await _sut.DeleteAccountAsync(Guid.NewGuid(), "x@x.com");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── DeleteAccountAsync (hard delete path via config flag) ────────────────

    [Fact]
    public async Task DeleteAccount_HardDeleteFlag_RemovesUserRow()
    {
        var sut = MakeSutWithConfig(new Dictionary<string, string?>
        {
            ["Account:HardDelete"] = "true",
        });
        var user = await CreateUserAsync();

        await sut.DeleteAccountAsync(user.Id, user.Email);

        (await _db.Users.FindAsync(user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAccount_HardDeleteFlagFalse_SoftDeletes()
    {
        var sut = MakeSutWithConfig(new Dictionary<string, string?>
        {
            ["Account:HardDelete"] = "false",
        });
        var user = await CreateUserAsync();

        await sut.DeleteAccountAsync(user.Id, user.Email);

        var updated = await _db.Users.FindAsync(user.Id);
        updated.Should().NotBeNull();
        updated!.DeletionScheduledAt.Should().NotBeNull();
    }
}
