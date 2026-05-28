using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

public class ChatServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly Mock<IMediaStorageService> _storageMock;
    private readonly Mock<IPrivacyService> _privacyMock;
    private readonly ChatService _sut;

    public ChatServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new OmnijoyDbContext(options);
        _storageMock = new Mock<IMediaStorageService>();
        _privacyMock = new Mock<IPrivacyService>();

        // By default allow messaging
        _privacyMock.Setup(p => p.CanSendMessagesAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(true);

        _sut = new ChatService(_db, _storageMock.Object, _privacyMock.Object);
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

    private async Task<(Conversation conversation, Guid aliceId, Guid bobId)> CreateDirectConversationAsync()
    {
        var alice = await CreateUserAsync("Alice");
        var bob   = await CreateUserAsync("Bob");

        var conv = new Conversation
        {
            Id        = Guid.NewGuid(),
            Type      = ConversationType.Direct,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Conversations.Add(conv);

        _db.ConversationParticipants.Add(new ConversationParticipant
        {
            ConversationId = conv.Id,
            UserId         = alice.Id,
            User           = alice,
            JoinedAt       = DateTime.UtcNow,
        });
        _db.ConversationParticipants.Add(new ConversationParticipant
        {
            ConversationId = conv.Id,
            UserId         = bob.Id,
            User           = bob,
            JoinedAt       = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
        return (conv, alice.Id, bob.Id);
    }

    // ── GetOrCreateDirectConversation ─────────────────────────────────────────

    [Fact]
    public async Task GetOrCreateDirect_NewConversation_CreatesAndReturnsDto()
    {
        var alice = await CreateUserAsync("Alice");
        var bob   = await CreateUserAsync("Bob");

        var dto = await _sut.GetOrCreateDirectConversationAsync(alice.Id, bob.Id);

        dto.Should().NotBeNull();
        dto.Type.Should().Be("Direct");
        dto.Participants.Should().HaveCount(2);

        var convId = await _db.Conversations.Select(c => c.Id).FirstAsync();
        var participants = await _db.ConversationParticipants.CountAsync();
        participants.Should().Be(2);

        _ = convId; // used implicitly
    }

    [Fact]
    public async Task GetOrCreateDirect_ExistingConversation_ReturnsExisting()
    {
        var (conv, aliceId, bobId) = await CreateDirectConversationAsync();

        var dto = await _sut.GetOrCreateDirectConversationAsync(aliceId, bobId);

        dto.Id.Should().Be(conv.Id);
        (await _db.Conversations.CountAsync()).Should().Be(1); // no duplicate created
    }

    [Fact]
    public async Task GetOrCreateDirect_SelfConversation_ThrowsArgumentException()
    {
        var alice = await CreateUserAsync("Alice");

        await _sut.Invoking(s => s.GetOrCreateDirectConversationAsync(alice.Id, alice.Id))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*yourself*");
    }

    [Fact]
    public async Task GetOrCreateDirect_PrivacyBlocked_ThrowsUnauthorized()
    {
        var alice = await CreateUserAsync("Alice");
        var bob   = await CreateUserAsync("Bob");

        _privacyMock.Setup(p => p.CanSendMessagesAsync(bob.Id, alice.Id))
            .ReturnsAsync(false);

        await _sut.Invoking(s => s.GetOrCreateDirectConversationAsync(alice.Id, bob.Id))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetOrCreateDirect_TargetNotFound_ThrowsKeyNotFound()
    {
        var alice = await CreateUserAsync("Alice");

        await _sut.Invoking(s => s.GetOrCreateDirectConversationAsync(alice.Id, Guid.NewGuid()))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── SendMessage ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SendMessage_TextMessage_StoresAndReturnsDto()
    {
        var (conv, aliceId, _) = await CreateDirectConversationAsync();

        var dto = await _sut.SendMessageAsync(conv.Id, aliceId, "Hello Bob!", null);

        dto.Should().NotBeNull();
        dto.Content.Should().Be("Hello Bob!");
        dto.MessageType.Should().Be("Text");
        dto.IsDeleted.Should().BeFalse();

        (await _db.Messages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SendMessage_NonParticipant_ThrowsUnauthorized()
    {
        var (conv, _, _) = await CreateDirectConversationAsync();
        var outsider = await CreateUserAsync("Outsider");

        await _sut.Invoking(s => s.SendMessageAsync(conv.Id, outsider.Id, "Sneak", null))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task SendMessage_EmptyContentNoMedia_ThrowsArgumentException()
    {
        var (conv, aliceId, _) = await CreateDirectConversationAsync();

        await _sut.Invoking(s => s.SendMessageAsync(conv.Id, aliceId, "", null))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*content or*");
    }

    [Fact]
    public async Task SendMessage_WithImageMedia_DetectsImageType()
    {
        var (conv, aliceId, _) = await CreateDirectConversationAsync();
        _storageMock
            .Setup(s => s.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), "messages/images"))
            .ReturnsAsync("/uploads/messages/images/photo.jpg");

        var media = new MediaUploadItem(new MemoryStream([0xFF, 0xD8]), "photo.jpg", "image/jpeg");

        var dto = await _sut.SendMessageAsync(conv.Id, aliceId, null, [media]);

        dto.MessageType.Should().Be("Image");
        dto.Media.Should().NotBeNull();
        dto.Media!.Url.Should().Be("/uploads/messages/images/photo.jpg");
    }

    // ── DeleteMessage ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteMessage_Owner_SoftDeletes()
    {
        var (conv, aliceId, _) = await CreateDirectConversationAsync();
        var sentDto = await _sut.SendMessageAsync(conv.Id, aliceId, "to delete", null);

        var deleted = await _sut.DeleteMessageAsync(sentDto.Id, aliceId);

        deleted.IsDeleted.Should().BeTrue();
        deleted.Content.Should().BeNull();

        var stored = await _db.Messages.FindAsync(sentDto.Id);
        stored!.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteMessage_NonOwner_ThrowsUnauthorized()
    {
        var (conv, aliceId, bobId) = await CreateDirectConversationAsync();
        var sentDto = await _sut.SendMessageAsync(conv.Id, aliceId, "Alice's message", null);

        await _sut.Invoking(s => s.DeleteMessageAsync(sentDto.Id, bobId))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*your own*");
    }

    [Fact]
    public async Task DeleteMessage_NotFound_ThrowsKeyNotFound()
    {
        await _sut.Invoking(s => s.DeleteMessageAsync(Guid.NewGuid(), Guid.NewGuid()))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── GetMessages ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMessages_ReturnsMessagesInAscendingOrder()
    {
        var (conv, aliceId, _) = await CreateDirectConversationAsync();

        for (int i = 0; i < 3; i++)
        {
            _db.Messages.Add(new Message
            {
                Id             = Guid.NewGuid(),
                ConversationId = conv.Id,
                SenderUserId   = aliceId,
                Content        = $"Message {i}",
                MessageType    = MessageType.Text,
                CreatedAt      = DateTime.UtcNow.AddSeconds(i),
            });
        }
        // Set Sender navigation property via tracked entity
        var alice = await _db.Users.FindAsync(aliceId);
        foreach (var msg in _db.Messages.Local)
            msg.Sender = alice!;

        await _db.SaveChangesAsync();

        var messages = await _sut.GetMessagesAsync(conv.Id, aliceId, null, 10);

        messages.Should().HaveCount(3);
        messages[0].Content.Should().Be("Message 0");
        messages[2].Content.Should().Be("Message 2");
    }

    [Fact]
    public async Task GetMessages_NonParticipant_ThrowsUnauthorized()
    {
        var (conv, _, _) = await CreateDirectConversationAsync();
        var outsider = await CreateUserAsync("Outsider");

        await _sut.Invoking(s => s.GetMessagesAsync(conv.Id, outsider.Id, null, 20))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── MarkConversationRead ──────────────────────────────────────────────────

    [Fact]
    public async Task MarkConversationRead_UpdatesLastReadAt()
    {
        var (conv, aliceId, _) = await CreateDirectConversationAsync();

        await _sut.MarkConversationReadAsync(conv.Id, aliceId);

        var participant = await _db.ConversationParticipants
            .FirstAsync(cp => cp.ConversationId == conv.Id && cp.UserId == aliceId);

        participant.LastReadAt.Should().NotBeNull();
        participant.LastReadAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ── IsParticipant / GetParticipantIds ─────────────────────────────────────

    [Fact]
    public async Task IsParticipant_ReturnsCorrectly()
    {
        var (conv, aliceId, _) = await CreateDirectConversationAsync();
        var outsider = await CreateUserAsync("Outsider");

        (await _sut.IsParticipantAsync(conv.Id, aliceId)).Should().BeTrue();
        (await _sut.IsParticipantAsync(conv.Id, outsider.Id)).Should().BeFalse();
    }
}
