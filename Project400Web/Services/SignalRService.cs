using Microsoft.AspNetCore.SignalR.Client;

namespace Project400Web.Services;

public class SignalRService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly string _hubUrl;
    private readonly ILogger<SignalRService> _logger;

    public event Func<string, string, Task>? UnlockRequestReceived;
    public event Func<string, string, Task>? UnlockStatusUpdate;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public SignalRService(IConfiguration configuration, ILogger<SignalRService> logger)
    {
        var apiUrl = configuration["ApiPublicUrl"] ?? "https://ca-project400-api.wittystone-4c147989.francecentral.azurecontainerapps.io";
        _hubUrl = $"{apiUrl}/hubs/unlock";
        _logger = logger;
    }

    public async Task StartAsync(string userId)
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<string, string>("UnlockRequestReceived", async (doorName, requestId) =>
        {
            _logger.LogInformation($"Unlock request received for {doorName}, RequestId: {requestId}");
            if (UnlockRequestReceived != null)
            {
                await UnlockRequestReceived.Invoke(doorName, requestId);
            }
        });

        _hubConnection.On<string, string>("UnlockStatusUpdate", async (status, doorName) =>
        {
            _logger.LogInformation($"Unlock status: {status} for {doorName}");
            if (UnlockStatusUpdate != null)
            {
                await UnlockStatusUpdate.Invoke(status, doorName);
            }
        });

        await _hubConnection.StartAsync();
        await _hubConnection.InvokeAsync("RegisterUser", userId);

        _logger.LogInformation($"SignalR connected for user {userId}");
    }

    public async Task StopAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
