using Microsoft.AspNetCore.SignalR;

namespace IntelliLog.API.Hubs;

public class AlertHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("Connected", $"Connected as {Context.ConnectionId}");
        await base.OnConnectedAsync();
    }
}
