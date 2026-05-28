using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
}
