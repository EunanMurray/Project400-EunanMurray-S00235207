using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Project400API.Hubs;

public class UnlockHub : Hub
{
    private static readonly Dictionary<string, string> UserConnections = new();
    private static readonly Dictionary<string, string> DeviceConnections = new();

    public async Task RegisterUser(string userId)
    {
        UserConnections[userId] = Context.ConnectionId;
        await Clients.Caller.SendAsync("Registered", userId);
    }

    public async Task RegisterDevice(string deviceId)
    {
        DeviceConnections[deviceId] = Context.ConnectionId;
        await Clients.Caller.SendAsync("DeviceRegistered", deviceId);
    }

    [Authorize(Policy = "Admin")]
    public async Task JoinAdminGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
        await Clients.Caller.SendAsync("JoinedAdminGroup");
    }

    public async Task SendUnlockRequestToUser(string userId, string doorName, string requestId)
    {
        if (UserConnections.TryGetValue(userId, out var connectionId))
        {
            await Clients.Client(connectionId).SendAsync("UnlockRequestReceived", doorName, requestId);
        }
    }

    public async Task SendUnlockCommandToDevice(string deviceId, bool approved)
    {
        if (DeviceConnections.TryGetValue(deviceId, out var connectionId))
        {
            await Clients.Client(connectionId).SendAsync("UnlockCommand", approved);
        }
    }

    public async Task NotifyUnlockStatus(string status, string doorName)
    {
        await Clients.All.SendAsync("UnlockStatusUpdate", status, doorName);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = UserConnections.FirstOrDefault(x => x.Value == Context.ConnectionId).Key;
        if (userId != null)
        {
            UserConnections.Remove(userId);
        }

        var deviceId = DeviceConnections.FirstOrDefault(x => x.Value == Context.ConnectionId).Key;
        if (deviceId != null)
        {
            DeviceConnections.Remove(deviceId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
