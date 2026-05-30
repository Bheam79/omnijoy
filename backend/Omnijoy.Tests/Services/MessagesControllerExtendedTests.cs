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

/// <summary>
/// Extended tests for <see cref="MessagesController"/>.SendMessage action,
/// covering the form-data path and notification fan-out.
/// </summary>
public class MessagesControllerExtendedTests
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
        // Pre-populate form so Request.Form.Files fallback doesn't throw.
        http.Request.Form = new FormCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(),
            new FormFileCollection());
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
        Content:        "Hello",
        MessageType:    "Text",
        Media:          null,
        CreatedAt:      DateTime.UtcNow,
        IsDeleted:      false
    );

    // ── SendMessage — happy path (text only, no file) ─────────────────────────

    [Fact]
    public async Task SendMessage_ReturnsCreated_WhenTextOnly()
    {
        var userId = Guid.NewGuid();
        var convId = Guid.NewGuid();
        var msgId  = Guid.NewGuid();
        var chat   = new Mock<IChatService>();
        var hub    = new Mock<IHubContext<ChatHub>>();
        var notifs = new Mock<INotificationService>();

        var message = SampleMessage(msgId, convId, userId);
        chat.Setup(c => c.SendMessageAsync(convId, userId, "Hello", null))
            .ReturnsAsync(message);
        chat.Setup(c => c.GetParticipantUserIdsAsync(convId))
            .ReturnsAsync(new[] { userId });
        notifs.Setup(n => n.PushTransientToManyAsync(It.IsAny<Guid[]>(), It.IsAny<string>(), It.IsAny<object>()))
              .Returns(Task.CompletedTask);

        var controller = Build(chat, hub, notifs, userId);

        var result = await controller.SendMessage(new SendMessageFormInput
        {
            ConversationId = convId.ToString(),
            Content        = "Hello",
        });

        result.Should().BeOfType<CreatedResult>();
        ((CreatedResult)result).Location.Should().Contain(msgId.ToString());
    }

    [Fact]
    public async Task SendMessage_PushesNotifications_WhenOtherParticipantsExist()
    {
        var userId      = Guid.NewGuid();
        var otherId     = Guid.NewGuid();
        var convId      = Guid.NewGuid();
        var msgId       = Guid.NewGuid();
        var chat        = new Mock<IChatService>();
        var hub         = new Mock<IHubContext<ChatHub>>();
        var notifs      = new Mock<INotificationService>();

        var message = SampleMessage(msgId, convId, userId);
        chat.Setup(c => c.SendMessageAsync(convId, userId, "Hi", null)).ReturnsAsync(message);
        chat.Setup(c => c.GetParticipantUserIdsAsync(convId)).ReturnsAsync(new[] { userId, otherId });
        notifs.Setup(n => n.PushTransientToManyAsync(It.IsAny<Guid[]>(), "MessageReceived", It.IsAny<object>()))
              .Returns(Task.CompletedTask);

        var controller = Build(chat, hub, notifs, userId);

        var result = await controller.SendMessage(new SendMessageFormInput
        {
            ConversationId = convId.ToString(),
            Content        = "Hi",
        });

        result.Should().BeOfType<CreatedResult>();
        notifs.Verify(n => n.PushTransientToManyAsync(
            It.Is<Guid[]>(ids => ids.Contains(otherId) && !ids.Contains(userId)),
            "MessageReceived",
            It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task SendMessage_Returns403_WhenNotParticipant()
    {
        var userId = Guid.NewGuid();
        var convId = Guid.NewGuid();
        var chat   = new Mock<IChatService>();
        var hub    = new Mock<IHubContext<ChatHub>>();
        var notifs = new Mock<INotificationService>();
        chat.Setup(c => c.SendMessageAsync(convId, userId, It.IsAny<string?>(), null))
            .ThrowsAsync(new UnauthorizedAccessException("not a participant"));
        var controller = Build(chat, hub, notifs, userId);

        var result = await controller.SendMessage(new SendMessageFormInput
        {
            ConversationId = convId.ToString(),
            Content        = "Hello",
        });

        ((ObjectResult)result).StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task SendMessage_ReturnsBadRequest_WhenArgumentException()
    {
        var userId = Guid.NewGuid();
        var convId = Guid.NewGuid();
        var chat   = new Mock<IChatService>();
        var hub    = new Mock<IHubContext<ChatHub>>();
        var notifs = new Mock<INotificationService>();
        chat.Setup(c => c.SendMessageAsync(convId, userId, It.IsAny<string?>(), null))
            .ThrowsAsync(new ArgumentException("invalid content"));
        var controller = Build(chat, hub, notifs, userId);

        var result = await controller.SendMessage(new SendMessageFormInput
        {
            ConversationId = convId.ToString(),
            Content        = null, // empty content
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── SendMessage — with attached file ─────────────────────────────────────

    [Fact]
    public async Task SendMessage_ReturnsCreated_WithFileAttachment()
    {
        var userId = Guid.NewGuid();
        var convId = Guid.NewGuid();
        var msgId  = Guid.NewGuid();
        var chat   = new Mock<IChatService>();
        var hub    = new Mock<IHubContext<ChatHub>>();
        var notifs = new Mock<INotificationService>();

        // Mock a file
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(512);
        mockFile.Setup(f => f.FileName).Returns("image.jpg");
        mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var message = SampleMessage(msgId, convId, userId);
        chat.Setup(c => c.SendMessageAsync(convId, userId, null, It.IsAny<List<Omnijoy.Core.DTOs.Posts.MediaUploadItem>?>()))
            .ReturnsAsync(message);
        chat.Setup(c => c.GetParticipantUserIdsAsync(convId)).ReturnsAsync(new[] { userId });
        notifs.Setup(n => n.PushTransientToManyAsync(It.IsAny<Guid[]>(), It.IsAny<string>(), It.IsAny<object>()))
              .Returns(Task.CompletedTask);

        var controller = Build(chat, hub, notifs, userId);

        var result = await controller.SendMessage(new SendMessageFormInput
        {
            ConversationId = convId.ToString(),
            File           = mockFile.Object,
        });

        result.Should().BeOfType<CreatedResult>();
    }
}
