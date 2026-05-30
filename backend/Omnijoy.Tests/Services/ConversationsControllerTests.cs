using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Omnijoy.Api.Controllers;
using Omnijoy.Api.Hubs;
using Omnijoy.Core.DTOs.Chat;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Tests.Services;

/// <summary>Unit tests for <see cref="ConversationsController"/>.</summary>
public class ConversationsControllerTests
{
    private static ConversationsController Build(
        Mock<IChatService>          chat,
        Mock<IHubContext<ChatHub>>  hub,
        Guid? userId)
    {
        var clients = new Mock<IHubClients>();
        var group   = new Mock<IClientProxy>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);
        hub.Setup(h => h.Clients).Returns(clients.Object);

        var controller = new ConversationsController(chat.Object, hub.Object);
        var http = new DefaultHttpContext();
        if (userId is { } id)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static ConversationDto SampleConversation(Guid id) => new(
        Id:           id,
        Type:         "Direct",
        Participants: Array.Empty<ChatUserDto>(),
        LastMessage:  null,
        UnreadCount:  0,
        CreatedAt:    DateTime.UtcNow
    );

    // ── GetConversations ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetConversations_ReturnsOk_WithList()
    {
        var userId = Guid.NewGuid();
        var chat   = new Mock<IChatService>();
        var hub    = new Mock<IHubContext<ChatHub>>();
        chat.Setup(c => c.GetConversationsAsync(userId))
            .ReturnsAsync(Array.Empty<ConversationDto>());
        var controller = Build(chat, hub, userId);

        var result = await controller.GetConversations();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetConversations_ReturnsUnauthorized_WhenNoUserId()
    {
        var chat   = new Mock<IChatService>();
        var hub    = new Mock<IHubContext<ChatHub>>();
        var controller = Build(chat, hub, null);

        var result = await controller.GetConversations();

        result.Should().BeOfType<UnauthorizedResult>();
        chat.Verify(c => c.GetConversationsAsync(It.IsAny<Guid>()), Times.Never);
    }

    // ── GetMessages ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMessages_ReturnsOk_WithList()
    {
        var userId = Guid.NewGuid();
        var convId = Guid.NewGuid();
        var chat   = new Mock<IChatService>();
        var hub    = new Mock<IHubContext<ChatHub>>();
        chat.Setup(c => c.GetMessagesAsync(convId, userId, null, 30))
            .ReturnsAsync(Array.Empty<MessageDto>());
        var controller = Build(chat, hub, userId);

        var result = await controller.GetMessages(convId, null, 30);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetMessages_ReturnsUnauthorized_WhenNoUserId()
    {
        var chat   = new Mock<IChatService>();
        var hub    = new Mock<IHubContext<ChatHub>>();
        var controller = Build(chat, hub, null);

        var result = await controller.GetMessages(Guid.NewGuid(), null, 30);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetMessages_Returns403_WhenNotParticipant()
    {
        var userId = Guid.NewGuid();
        var convId = Guid.NewGuid();
        var chat   = new Mock<IChatService>();
        var hub    = new Mock<IHubContext<ChatHub>>();
        chat.Setup(c => c.GetMessagesAsync(convId, userId, null, 30))
            .ThrowsAsync(new UnauthorizedAccessException("not a participant"));
        var controller = Build(chat, hub, userId);

        var result = await controller.GetMessages(convId, null, 30);

        ((ObjectResult)result).StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task GetMessages_ClampsLimit_WhenOutOfRange()
    {
        var userId = Guid.NewGuid();
        var convId = Guid.NewGuid();
        var chat   = new Mock<IChatService>();
        var hub    = new Mock<IHubContext<ChatHub>>();
        chat.Setup(c => c.GetMessagesAsync(convId, userId, null, 30))
            .ReturnsAsync(Array.Empty<MessageDto>());
        var controller = Build(chat, hub, userId);

        // limit=0 is out of range, should be clamped to 30
        var result = await controller.GetMessages(convId, null, 0);

        result.Should().BeOfType<OkObjectResult>();
        chat.Verify(c => c.GetMessagesAsync(convId, userId, null, 30), Times.Once);
    }

    // ── GetOrCreateDirect ────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrCreateDirect_ReturnsOk_OnSuccess()
    {
        var myId   = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var convId  = Guid.NewGuid();
        var chat    = new Mock<IChatService>();
        var hub     = new Mock<IHubContext<ChatHub>>();
        chat.Setup(c => c.GetOrCreateDirectConversationAsync(myId, otherId))
            .ReturnsAsync(SampleConversation(convId));
        var controller = Build(chat, hub, myId);

        var result = await controller.GetOrCreateDirect(otherId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetOrCreateDirect_ReturnsUnauthorized_WhenNoUserId()
    {
        var chat   = new Mock<IChatService>();
        var hub    = new Mock<IHubContext<ChatHub>>();
        var controller = Build(chat, hub, null);

        var result = await controller.GetOrCreateDirect(Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetOrCreateDirect_ReturnsBadRequest_WhenArgumentException()
    {
        var myId    = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var chat    = new Mock<IChatService>();
        var hub     = new Mock<IHubContext<ChatHub>>();
        chat.Setup(c => c.GetOrCreateDirectConversationAsync(myId, otherId))
            .ThrowsAsync(new ArgumentException("can't message self"));
        var controller = Build(chat, hub, myId);

        var result = await controller.GetOrCreateDirect(otherId);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── MarkRead ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task MarkRead_ReturnsNoContent_OnSuccess()
    {
        var userId = Guid.NewGuid();
        var convId = Guid.NewGuid();
        var chat   = new Mock<IChatService>();
        var hub    = new Mock<IHubContext<ChatHub>>();
        chat.Setup(c => c.MarkConversationReadAsync(convId, userId)).Returns(Task.CompletedTask);
        var controller = Build(chat, hub, userId);

        var result = await controller.MarkRead(convId);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task MarkRead_ReturnsUnauthorized_WhenNoUserId()
    {
        var chat   = new Mock<IChatService>();
        var hub    = new Mock<IHubContext<ChatHub>>();
        var controller = Build(chat, hub, null);

        var result = await controller.MarkRead(Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedResult>();
    }
}
