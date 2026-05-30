using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Omnijoy.Api.Hubs;

namespace Omnijoy.Tests.Services;

/// <summary>Unit tests for <see cref="NotificationHubDispatcher"/>.</summary>
public class NotificationHubDispatcherTests
{
    private static (NotificationHubDispatcher, Mock<IClientProxy>) Build()
    {
        var proxy   = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);

        var hub = new Mock<IHubContext<NotificationHub>>();
        hub.Setup(h => h.Clients).Returns(clients.Object);

        return (new NotificationHubDispatcher(hub.Object), proxy);
    }

    [Fact]
    public async Task SendToUserAsync_CallsSendAsync_OnUserGroup()
    {
        var (dispatcher, proxy) = Build();
        var userId = Guid.NewGuid();

        await dispatcher.SendToUserAsync(userId, "TestEvent", new { data = "hello" });

        proxy.Verify(p => p.SendCoreAsync(
            "TestEvent",
            It.Is<object[]>(a => a.Length == 1),
            default), Times.Once);
    }

    [Fact]
    public async Task SendToGroupAsync_CallsSendAsync_OnNamedGroup()
    {
        var (dispatcher, proxy) = Build();

        await dispatcher.SendToGroupAsync("stream:abc", "LiveEvent", new { count = 5 });

        proxy.Verify(p => p.SendCoreAsync(
            "LiveEvent",
            It.Is<object[]>(a => a.Length == 1),
            default), Times.Once);
    }
}
