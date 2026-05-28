using Microsoft.AspNetCore.SignalR;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Api.Hubs;

/// <summary>
/// Bridges <see cref="IHubContextDispatcher"/> (declared in the Infrastructure
/// project, which can't reference the API hubs directly) to the actual
/// <see cref="NotificationHub"/> instance running in this process.
///
/// All "send to user" calls fan out to the SignalR group named
/// <c>user:{userId}</c>, which every authenticated NotificationHub connection
/// joins automatically.
/// </summary>
public class NotificationHubDispatcher : IHubContextDispatcher
{
    private readonly IHubContext<NotificationHub> _hub;

    public NotificationHubDispatcher(IHubContext<NotificationHub> hub)
    {
        _hub = hub;
    }

    public Task SendToUserAsync(Guid userId, string eventName, object payload)
        => _hub.Clients.Group($"user:{userId}").SendAsync(eventName, payload);

    public Task SendToGroupAsync(string groupName, string eventName, object payload)
        => _hub.Clients.Group(groupName).SendAsync(eventName, payload);
}
