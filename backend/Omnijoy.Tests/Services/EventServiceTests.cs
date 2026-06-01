using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Omnijoy.Core.DTOs.Events;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;
using System.IO;

namespace Omnijoy.Tests.Services;

public class EventServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly Mock<IMediaStorageService> _storageMock;
    private readonly Mock<IImageProcessingService> _imageProcessorMock;
    private readonly EventService _sut;

    public EventServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new OmnijoyDbContext(options);
        _storageMock = new Mock<IMediaStorageService>();
        _imageProcessorMock = new Mock<IImageProcessingService>();
        _imageProcessorMock
            .Setup(p => p.ProcessImageAsync(It.IsAny<Stream>(), It.IsAny<ImageFolder>()))
            .ReturnsAsync((Stream s, ImageFolder _) => s);
        _sut = new EventService(_db, _storageMock.Object, _imageProcessorMock.Object);
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

    private async Task MakeFriendsAsync(Guid a, Guid b)
    {
        _db.Friends.Add(new Friend
        {
            Id          = Guid.NewGuid(),
            RequesterId = a,
            AddresseeId = b,
            Status      = FriendStatus.Accepted,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    private CreateEventRequest DefaultRequest(string privacy = "Everyone") => new CreateEventRequest(
        Title:         "Test Event",
        Description:   "A test event",
        StartAt:       DateTime.UtcNow.AddDays(7),
        EndAt:         null,
        Location:      "Online",
        Privacy:       privacy,
        PostingPolicy: null,
        CompanyPageId: null
    );

    // ── Helpers (company pages) ───────────────────────────────────────────────

    private async Task<CompanyPage> CreateCompanyPageAsync(Guid ownerId, string name = "Acme Corp")
    {
        var page = new CompanyPage
        {
            Id                = Guid.NewGuid(),
            Name              = name,
            CreatedByUserId   = ownerId,
            CreatedAt         = DateTime.UtcNow,
        };
        _db.CompanyPages.Add(page);
        _db.CompanyPageAdmins.Add(new CompanyPageAdmin
        {
            CompanyPageId = page.Id,
            UserId        = ownerId,
            Role          = CompanyPageRole.Owner,
        });
        await _db.SaveChangesAsync();
        return page;
    }

    // ── CreateEvent ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateEvent_ValidRequest_ReturnsDto()
    {
        var user = await CreateUserAsync("Alice");

        var dto = await _sut.CreateEventAsync(user.Id, DefaultRequest(), null);

        dto.Should().NotBeNull();
        dto.Title.Should().Be("Test Event");
        dto.Privacy.Should().Be("Everyone");
        dto.Creator.Id.Should().Be(user.Id);
        dto.GoingCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateEvent_InvalidPrivacy_ThrowsArgumentException()
    {
        var user = await CreateUserAsync();

        await _sut.Invoking(s => s.CreateEventAsync(user.Id,
            new CreateEventRequest("Title", null, DateTime.UtcNow.AddDays(1), null, null, "BadPrivacy", null, null), null))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Privacy*");
    }

    [Fact]
    public async Task CreateEvent_EmptyTitle_ThrowsArgumentException()
    {
        var user = await CreateUserAsync();

        await _sut.Invoking(s => s.CreateEventAsync(user.Id,
            new CreateEventRequest("   ", null, DateTime.UtcNow.AddDays(1), null, null, "Everyone", null, null), null))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Title*");
    }

    [Fact]
    public async Task CreateEvent_WithCoverImage_StoresCoverAndReturnsUrl()
    {
        var user = await CreateUserAsync();
        _storageMock
            .Setup(s => s.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), "events/covers"))
            .ReturnsAsync("/uploads/events/covers/cover.jpg");

        var cover = new CoverImageUploadItem(new MemoryStream([0xFF, 0xD8]), "cover.jpg", "image/jpeg");

        var dto = await _sut.CreateEventAsync(user.Id, DefaultRequest(), cover);

        dto.CoverImageUrl.Should().Be("/uploads/events/covers/cover.jpg");
    }

    [Fact]
    public async Task CreateEvent_WithCompanyPage_AsAdmin_Succeeds()
    {
        var owner = await CreateUserAsync("Owner");
        var page  = await CreateCompanyPageAsync(owner.Id);

        var request = new CreateEventRequest(
            Title:         "Company Event",
            Description:   null,
            StartAt:       DateTime.UtcNow.AddDays(3),
            EndAt:         null,
            Location:      "HQ",
            Privacy:       "Everyone",
            PostingPolicy: null,
            CompanyPageId: page.Id
        );

        var dto = await _sut.CreateEventAsync(owner.Id, request, null);

        dto.CompanyPageId.Should().Be(page.Id);
        dto.Title.Should().Be("Company Event");
    }

    [Fact]
    public async Task CreateEvent_WithCompanyPage_AsNonMember_ThrowsUnauthorized()
    {
        var owner   = await CreateUserAsync("Owner");
        var other   = await CreateUserAsync("Stranger");
        var page    = await CreateCompanyPageAsync(owner.Id);

        var request = new CreateEventRequest(
            Title:         "Hack Event",
            Description:   null,
            StartAt:       DateTime.UtcNow.AddDays(3),
            EndAt:         null,
            Location:      null,
            Privacy:       "Everyone",
            PostingPolicy: null,
            CompanyPageId: page.Id
        );

        await _sut.Invoking(s => s.CreateEventAsync(other.Id, request, null))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*admin*");
    }

    [Fact]
    public async Task CreateEvent_WithNonExistentCompanyPage_ThrowsKeyNotFound()
    {
        var user = await CreateUserAsync();

        var request = new CreateEventRequest(
            Title:         "Ghost Company Event",
            Description:   null,
            StartAt:       DateTime.UtcNow.AddDays(3),
            EndAt:         null,
            Location:      null,
            Privacy:       "Everyone",
            PostingPolicy: null,
            CompanyPageId: Guid.NewGuid()
        );

        await _sut.Invoking(s => s.CreateEventAsync(user.Id, request, null))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── GetEvent ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetEvent_PublicEvent_ReturnsDto()
    {
        var user = await CreateUserAsync();
        var created = await _sut.CreateEventAsync(user.Id, DefaultRequest("Everyone"), null);

        var dto = await _sut.GetEventAsync(created.Id, null);

        dto.Title.Should().Be("Test Event");
    }

    [Fact]
    public async Task GetEvent_FriendsOnlyEvent_NonFriendCannotView()
    {
        var creator  = await CreateUserAsync("Creator");
        var stranger = await CreateUserAsync("Stranger");
        var created  = await _sut.CreateEventAsync(creator.Id, DefaultRequest("Friends"), null);

        await _sut.Invoking(s => s.GetEventAsync(created.Id, stranger.Id))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetEvent_FriendsOnlyEvent_FriendCanView()
    {
        var creator = await CreateUserAsync("Creator");
        var friend  = await CreateUserAsync("Friend");
        await MakeFriendsAsync(creator.Id, friend.Id);

        var created = await _sut.CreateEventAsync(creator.Id, DefaultRequest("Friends"), null);

        var dto = await _sut.GetEventAsync(created.Id, friend.Id);
        dto.Title.Should().Be("Test Event");
    }

    [Fact]
    public async Task GetEvent_CreatorAlwaysHasAccess()
    {
        var creator = await CreateUserAsync("Creator");
        var created = await _sut.CreateEventAsync(creator.Id, DefaultRequest("OnlyMe"), null);

        var dto = await _sut.GetEventAsync(created.Id, creator.Id);
        dto.Title.Should().Be("Test Event");
    }

    [Fact]
    public async Task GetEvent_NotFound_ThrowsKeyNotFound()
    {
        await _sut.Invoking(s => s.GetEventAsync(Guid.NewGuid(), null))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── UpdateEvent ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateEvent_ByCreator_UpdatesFields()
    {
        var creator = await CreateUserAsync("Creator");
        var created = await _sut.CreateEventAsync(creator.Id, DefaultRequest(), null);

        var updated = await _sut.UpdateEventAsync(
            created.Id, creator.Id,
            new UpdateEventRequest("New Title", "New desc", null, null, "New Location", null, null),
            null);

        updated.Title.Should().Be("New Title");
        updated.Location.Should().Be("New Location");
    }

    [Fact]
    public async Task UpdateEvent_NonCreator_ThrowsUnauthorized()
    {
        var creator = await CreateUserAsync("Creator");
        var other   = await CreateUserAsync("Other");
        var created = await _sut.CreateEventAsync(creator.Id, DefaultRequest(), null);

        await _sut.Invoking(s => s.UpdateEventAsync(
                created.Id, other.Id, new UpdateEventRequest("Hack", null, null, null, null, null, null), null))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── DeleteEvent ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteEvent_ByCreator_RemovesFromDb()
    {
        var creator = await CreateUserAsync("Creator");
        var created = await _sut.CreateEventAsync(creator.Id, DefaultRequest(), null);

        await _sut.DeleteEventAsync(created.Id, creator.Id);

        (await _db.Events.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeleteEvent_NonCreator_ThrowsUnauthorized()
    {
        var creator = await CreateUserAsync("Creator");
        var other   = await CreateUserAsync("Other");
        var created = await _sut.CreateEventAsync(creator.Id, DefaultRequest(), null);

        await _sut.Invoking(s => s.DeleteEventAsync(created.Id, other.Id))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── RSVP ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Rsvp_Going_RecordsAttendee()
    {
        var creator = await CreateUserAsync("Creator");
        var guest   = await CreateUserAsync("Guest");
        var created = await _sut.CreateEventAsync(creator.Id, DefaultRequest(), null);

        var dto = await _sut.RsvpAsync(created.Id, guest.Id, "Going");

        dto.GoingCount.Should().Be(1);
        dto.MyRsvp.Should().Be("Going");
    }

    [Fact]
    public async Task Rsvp_Maybe_UpdatesStatus()
    {
        var creator = await CreateUserAsync("Creator");
        var guest   = await CreateUserAsync("Guest");
        var created = await _sut.CreateEventAsync(creator.Id, DefaultRequest(), null);

        await _sut.RsvpAsync(created.Id, guest.Id, "Going");
        var updated = await _sut.RsvpAsync(created.Id, guest.Id, "Maybe");

        updated.MaybeCount.Should().Be(1);
        updated.GoingCount.Should().Be(0);
        updated.MyRsvp.Should().Be("Maybe");
    }

    [Fact]
    public async Task Rsvp_InvalidStatus_ThrowsArgumentException()
    {
        var creator = await CreateUserAsync("Creator");
        var created = await _sut.CreateEventAsync(creator.Id, DefaultRequest(), null);

        await _sut.Invoking(s => s.RsvpAsync(created.Id, creator.Id, "HereOrNot"))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*RSVP*");
    }

    // ── GetAttendees ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAttendees_SegregatesByRsvpStatus()
    {
        var creator = await CreateUserAsync("Creator");
        var going   = await CreateUserAsync("Going");
        var maybe   = await CreateUserAsync("Maybe");
        var created = await _sut.CreateEventAsync(creator.Id, DefaultRequest(), null);

        await _sut.RsvpAsync(created.Id, going.Id,  "Going");
        await _sut.RsvpAsync(created.Id, maybe.Id,  "Maybe");

        var result = await _sut.GetAttendeesAsync(created.Id, creator.Id);

        result.Going.Should().HaveCount(1);
        result.Going[0].UserId.Should().Be(going.Id);
        result.Maybe.Should().HaveCount(1);
        result.NotGoing.Should().BeEmpty();
    }
}
