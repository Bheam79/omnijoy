using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Omnijoy.Core.DTOs.Users;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;
using System.IO;

namespace Omnijoy.Tests;

public class UserServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly Mock<IMediaStorageService> _storageMock;
    private readonly Mock<IImageProcessingService> _imageProcessorMock;
    private readonly UserService _sut;

    public UserServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new OmnijoyDbContext(options);
        _storageMock = new Mock<IMediaStorageService>();
        _imageProcessorMock = new Mock<IImageProcessingService>();
        // Return the input stream unchanged so storage mock still receives a readable stream.
        _imageProcessorMock
            .Setup(p => p.ProcessImageAsync(It.IsAny<Stream>(), It.IsAny<ImageFolder>()))
            .ReturnsAsync((Stream s, ImageFolder _) => s);
        var privacy = new PrivacyService(_db);
        _sut = new UserService(_db, _storageMock.Object, privacy, _imageProcessorMock.Object);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<User> CreateUserAsync(
        string displayName = "Test User",
        bool showBirthDate = false,
        PrivacyLevel profilePrivacy = PrivacyLevel.Everyone)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@test.com",
            DisplayName = displayName,
            Gender = Gender.NotDisclosed,
            BirthDate = new DateOnly(1990, 6, 15),
            ShowBirthDate = showBirthDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        user.PrivacySettings = new UserPrivacySettings
        {
            UserId = user.Id,
            WhoCanSeeProfile = profilePrivacy,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task MakeFriendsAsync(Guid a, Guid b)
    {
        _db.Friends.Add(new Friend
        {
            Id = Guid.NewGuid(),
            RequesterId = a,
            AddresseeId = b,
            Status = FriendStatus.Accepted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    // ── GetUserProfileAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetUserProfile_OwnProfile_ReturnsIsOwnProfile()
    {
        var user = await CreateUserAsync(showBirthDate: false);

        var result = await _sut.GetUserProfileAsync(user.Id, user.Id);

        result.IsOwnProfile.Should().BeTrue();
        result.BirthDate.Should().Be("1990-06-15"); // owner always sees birth date
    }

    [Fact]
    public async Task GetUserProfile_ShowBirthDateTrue_BirthDateVisibleToOthers()
    {
        var owner = await CreateUserAsync(showBirthDate: true);
        var viewer = await CreateUserAsync();

        var result = await _sut.GetUserProfileAsync(owner.Id, viewer.Id);

        result.BirthDate.Should().Be("1990-06-15");
    }

    [Fact]
    public async Task GetUserProfile_ShowBirthDateFalse_BirthDateHiddenFromOthers()
    {
        var owner = await CreateUserAsync(showBirthDate: false);
        var viewer = await CreateUserAsync();

        var result = await _sut.GetUserProfileAsync(owner.Id, viewer.Id);

        result.BirthDate.Should().BeNull();
    }

    [Fact]
    public async Task GetUserProfile_PrivateProfile_ThrowsForNonFriend()
    {
        var owner = await CreateUserAsync(profilePrivacy: PrivacyLevel.Friends);
        var stranger = await CreateUserAsync();

        var act = async () => await _sut.GetUserProfileAsync(owner.Id, stranger.Id);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetUserProfile_PrivateProfile_SucceedsForFriend()
    {
        var owner = await CreateUserAsync(profilePrivacy: PrivacyLevel.Friends);
        var friend = await CreateUserAsync();
        await MakeFriendsAsync(owner.Id, friend.Id);

        var result = await _sut.GetUserProfileAsync(owner.Id, friend.Id);

        result.IsFriend.Should().BeTrue();
        result.DisplayName.Should().Be(owner.DisplayName);
    }

    [Fact]
    public async Task GetUserProfile_OnlyMeProfile_ThrowsForOthers()
    {
        var owner = await CreateUserAsync(profilePrivacy: PrivacyLevel.OnlyMe);
        var viewer = await CreateUserAsync();

        var act = async () => await _sut.GetUserProfileAsync(owner.Id, viewer.Id);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetUserProfile_FriendCount_ReflectsAcceptedFriendships()
    {
        var user = await CreateUserAsync();
        var f1 = await CreateUserAsync();
        var f2 = await CreateUserAsync();
        await MakeFriendsAsync(user.Id, f1.Id);
        await MakeFriendsAsync(f2.Id, user.Id);

        var result = await _sut.GetUserProfileAsync(user.Id, user.Id);

        result.FriendCount.Should().Be(2);
    }

    [Fact]
    public async Task GetUserProfile_MutualFriends_ComputedCorrectly()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var charlie = await CreateUserAsync("Charlie");

        await MakeFriendsAsync(alice.Id, charlie.Id);
        await MakeFriendsAsync(bob.Id, charlie.Id);

        var result = await _sut.GetUserProfileAsync(bob.Id, alice.Id);

        result.MutualFriendCount.Should().Be(1);
    }

    [Fact]
    public async Task GetUserProfile_NotFound_ThrowsKeyNotFound()
    {
        var act = async () => await _sut.GetUserProfileAsync(Guid.NewGuid(), null);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── UpdateProfileAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateProfile_ValidDisplayName_Updates()
    {
        var user = await CreateUserAsync();

        var result = await _sut.UpdateProfileAsync(user.Id, new UpdateProfileRequest(
            DisplayName: "New Name",
            Bio: null,
            Gender: null,
            BirthDate: null,
            ShowBirthDate: null));

        result.DisplayName.Should().Be("New Name");
    }

    [Fact]
    public async Task UpdateProfile_UpdatesBio()
    {
        var user = await CreateUserAsync();

        var result = await _sut.UpdateProfileAsync(user.Id, new UpdateProfileRequest(
            DisplayName: null,
            Bio: "Hello world!",
            Gender: null,
            BirthDate: null,
            ShowBirthDate: null));

        result.Bio.Should().Be("Hello world!");
    }

    [Fact]
    public async Task UpdateProfile_EmptyBio_ClearsBio()
    {
        var user = await CreateUserAsync();
        // First set a bio
        await _sut.UpdateProfileAsync(user.Id, new UpdateProfileRequest(
            null, "Initial bio", null, null, null));

        var result = await _sut.UpdateProfileAsync(user.Id, new UpdateProfileRequest(
            null, "   ", null, null, null));

        result.Bio.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProfile_ValidGender_Updates()
    {
        var user = await CreateUserAsync();

        var result = await _sut.UpdateProfileAsync(user.Id, new UpdateProfileRequest(
            null, null, "Female", null, null));

        result.Gender.Should().Be("Female");
    }

    [Fact]
    public async Task UpdateProfile_InvalidGender_ThrowsArgumentException()
    {
        var user = await CreateUserAsync();

        var act = async () => await _sut.UpdateProfileAsync(user.Id, new UpdateProfileRequest(
            null, null, "InvalidGender", null, null));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*InvalidGender*");
    }

    [Fact]
    public async Task UpdateProfile_ValidBirthDate_Updates()
    {
        var user = await CreateUserAsync();

        var result = await _sut.UpdateProfileAsync(user.Id, new UpdateProfileRequest(
            null, null, null, "2000-01-15", null));

        result.BirthDate.Should().Be("2000-01-15");
    }

    [Fact]
    public async Task UpdateProfile_EmptyBirthDate_ClearsBirthDate()
    {
        var user = await CreateUserAsync();

        var result = await _sut.UpdateProfileAsync(user.Id, new UpdateProfileRequest(
            null, null, null, "", null));

        result.BirthDate.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProfile_InvalidBirthDateFormat_ThrowsArgumentException()
    {
        var user = await CreateUserAsync();

        var act = async () => await _sut.UpdateProfileAsync(user.Id, new UpdateProfileRequest(
            null, null, null, "not-a-date", null));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*yyyy-MM-dd*");
    }

    [Fact]
    public async Task UpdateProfile_DisplayNameTooLong_ThrowsArgumentException()
    {
        var user = await CreateUserAsync();
        var longName = new string('A', 129);

        var act = async () => await _sut.UpdateProfileAsync(user.Id, new UpdateProfileRequest(
            longName, null, null, null, null));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateProfile_UserNotFound_ThrowsKeyNotFound()
    {
        var act = async () => await _sut.UpdateProfileAsync(Guid.NewGuid(), new UpdateProfileRequest(
            "Name", null, null, null, null));

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── UploadAvatarAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAvatar_CallsStorageAndUpdatesUrl()
    {
        var user = await CreateUserAsync();
        var stream = new MemoryStream([0x89, 0x50, 0x4E, 0x47]); // fake PNG bytes

        _storageMock
            .Setup(s => s.StoreAsync(It.IsAny<Stream>(), "avatar.webp", "avatars"))
            .ReturnsAsync("/uploads/avatars/new-file.webp");

        var result = await _sut.UploadAvatarAsync(user.Id, stream, "avatar.png");

        result.AvatarUrl.Should().Be("/uploads/avatars/new-file.webp");
        _storageMock.Verify(s => s.StoreAsync(It.IsAny<Stream>(), "avatar.webp", "avatars"), Times.Once);
    }

    [Fact]
    public async Task UploadAvatar_DeletesOldAvatar()
    {
        var user = await CreateUserAsync();
        user.AvatarUrl = "/uploads/avatars/old.png";
        await _db.SaveChangesAsync();

        var stream = new MemoryStream([0xFF]);
        _storageMock.Setup(s => s.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), "avatars"))
                    .ReturnsAsync("/uploads/avatars/new.png");

        await _sut.UploadAvatarAsync(user.Id, stream, "new.png");

        _storageMock.Verify(s => s.DeleteAsync("/uploads/avatars/old.png"), Times.Once);
    }

    // ── UploadCoverAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task UploadCover_CallsStorageAndUpdatesUrl()
    {
        var user = await CreateUserAsync();
        var stream = new MemoryStream([0xFF, 0xD8]); // fake JPEG bytes

        _storageMock
            .Setup(s => s.StoreAsync(It.IsAny<Stream>(), "cover.webp", "covers"))
            .ReturnsAsync("/uploads/covers/new-cover.webp");

        var result = await _sut.UploadCoverAsync(user.Id, stream, "cover.jpg");

        result.CoverUrl.Should().Be("/uploads/covers/new-cover.webp");
    }

    // ── Privacy settings ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetPrivacySettings_ReturnsDefaults()
    {
        var user = await CreateUserAsync();

        var result = await _sut.GetPrivacySettingsAsync(user.Id);

        result.WhoCanSeeProfile.Should().Be("Everyone");
        result.WhoCanSeePosts.Should().Be("Friends");
        result.WhoCanSeeFollowers.Should().Be("Friends");
    }

    [Fact]
    public async Task UpdatePrivacySettings_UpdatesSpecifiedFields()
    {
        var user = await CreateUserAsync();

        var result = await _sut.UpdatePrivacySettingsAsync(user.Id, new UpdatePrivacyRequest(
            WhoCanSeePosts: "OnlyMe",
            WhoCanSeeProfile: "Friends",
            WhoCanSendMessages: null,
            WhoCanSeeFriendList: null,
            WhoCanSeeEvents: null,
            WhoCanTagInPosts: null,
            WhoCanSeeFollowers: "OnlyMe"));

        result.WhoCanSeePosts.Should().Be("OnlyMe");
        result.WhoCanSeeProfile.Should().Be("Friends");
        result.WhoCanSendMessages.Should().Be("Friends"); // unchanged default
        result.WhoCanSeeFollowers.Should().Be("OnlyMe");
    }

    [Fact]
    public async Task UpdatePrivacySettings_InvalidValue_ThrowsArgumentException()
    {
        var user = await CreateUserAsync();

        var act = async () => await _sut.UpdatePrivacySettingsAsync(user.Id, new UpdatePrivacyRequest(
            WhoCanSeePosts: "PublicOnly",  // invalid
            null, null, null, null, null));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*PublicOnly*");
    }
}
