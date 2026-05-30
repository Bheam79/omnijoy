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

/// <summary>Unit tests for <see cref="MessagesController"/>.</summary>
public class MessagesControllerTests
{
    private static MessagesController Build(
        Mock<IChatService>          chat,
        Mock<IHubContext<ChatHub>>  hub,
        Mock<INotificationService>  notifs,
        Guid? userId)
    {
        var clients = new Mock<IHubClients>();
        var proxy   = new Mock<IClientProxy>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
        hub.Setup(h => h.Clients).Returns(clients.Object);

        var controller = new MessagesController(chat.Object, hub.Object, notifs.Object);

        var http = new DefaultHttpContext();
        if (userId is { } id)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static MessageDto SampleMessage(Guid id, Guid convId, Guid senderId) => new(
        Id:             id,
        ConversationId: convId,
        Sender:         new ChatUserDto(senderId, "Alice", null),
        Content:        "Hello!",
        MessageType:    "Text",
        Media:          null,
        CreatedAt:      DateTime.UtcNow,
        IsDeleted:      false
    );

    // ── DeleteMessage ────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteMessage_ReturnsOk_OnSuccess()
    {
        var userId    = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var convId    = Guid.NewGuid();
        var chat      = new Mock<IChatService>();
        var hub       = new Mock<IHubContext<ChatHub>>();
        var notifs    = new Mock<INotificationService>();
        chat.Setup(c => c.DeleteMessageAsync(messageId, userId))
            .ReturnsAsync(SampleMessage(messageId, convId, userId));
        var controller = Build(chat, hub, notifs, userId);

        var result = await controller.DeleteMessage(messageId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DeleteMessage_ReturnsUnauthorized_WhenNoUserId()
    {
        var chat   = new Mock<IChatService>();
        var hub    = new Mock<IHubContext<ChatHub>>();
        var notifs = new Mock<INotificationService>();
        var controller = Build(chat, hub, notifs, null);

        var result = await controller.DeleteMessage(Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task DeleteMessage_ReturnsNotFound_WhenMessageMissing()
    {
        var userId    = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var chat      = new Mock<IChatService>();
        var hub       = new Mock<IHubContext<ChatHub>>();
        var notifs    = new Mock<INotificationService>();
        chat.Setup(c => c.DeleteMessageAsync(messageId, userId))
            .ThrowsAsync(new KeyNotFoundException("not found"));
        var controller = Build(chat, hub, notifs, userId);

        var result = await controller.DeleteMessage(messageId);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteMessage_Returns403_WhenNotSender()
    {
        var userId    = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var chat      = new Mock<IChatService>();
        var hub       = new Mock<IHubContext<ChatHub>>();
        var notifs    = new Mock<INotificationService>();
        chat.Setup(c => c.DeleteMessageAsync(messageId, userId))
            .ThrowsAsync(new UnauthorizedAccessException("not sender"));
        var controller = Build(chat, hub, notifs, userId);

        var result = await controller.DeleteMessage(messageId);

        ((ObjectResult)result).StatusCode.Should().Be(403);
    }

    // ── SendMessage (basic validation tests, no file upload) ─────────────────

    [Fact]
    public async Task SendMessage_ReturnsUnauthorized_WhenNoUserId()
    {
        var chat   = new Mock<IChatService>();
        var hub    = new Mock<IHubContext<ChatHub>>();
        var notifs = new Mock<INotificationService>();
        var controller = Build(chat, hub, notifs, null);

        var result = await controller.SendMessage(new SendMessageFormInput
        {
            ConversationId = Guid.NewGuid().ToString(),
            Content        = "Hello",
        });

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task SendMessage_ReturnsBadRequest_WhenConversationIdInvalid()
    {
        var userId = Guid.NewGuid();
        var chat   = new Mock<IChatService>();
        var hub    = new Mock<IHubContext<ChatHub>>();
        var notifs = new Mock<INotificationService>();
        var controller = Build(chat, hub, notifs, userId);

        var result = await controller.SendMessage(new SendMessageFormInput
        {
            ConversationId = "not-a-guid",
            Content        = "Hello",
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
