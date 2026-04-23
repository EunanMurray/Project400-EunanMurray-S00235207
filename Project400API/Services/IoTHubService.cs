using Microsoft.Azure.Devices;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Project400API.Hubs;
using Project400API.Data;
using Project400API.Repositories.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Project400.Shared.Models.Device;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;

namespace Project400API.Services;

public class IoTHubMessage
{
    public string DeviceId { get; set; } = string.Empty;
    public string CardUid { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
}

public class IoTHubService : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<UnlockHub> _hubContext;
    private readonly ILogger<IoTHubService> _logger;
    private ServiceClient? _serviceClient;
    private CancellationTokenSource? _cts;
    private Task? _d2cListenerTask;

    public IoTHubService(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        IHubContext<UnlockHub> hubContext,
        ILogger<IoTHubService> logger)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var connectionString = _configuration["Azure:IoTHub:ConnectionString"];
        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogWarning("IoT Hub connection string not configured");
            return Task.CompletedTask;
        }

        _serviceClient = ServiceClient.CreateFromConnectionString(connectionString);
        _cts = new CancellationTokenSource();

        var eventHubEndpoint = _configuration["Azure:IoTHub:EventHubEndpoint"];
        var eventHubName = _configuration["Azure:IoTHub:EventHubName"];

        if (!string.IsNullOrEmpty(eventHubEndpoint) && !string.IsNullOrEmpty(eventHubName))
        {
            _d2cListenerTask = Task.Run(() => ListenForD2CMessages(eventHubEndpoint, eventHubName, _cts.Token));
            _logger.LogInformation("D2C message listener started");
        }
        else
        {
            _logger.LogWarning("Event Hub endpoint/name not configured - D2C listener disabled");
        }

        _logger.LogInformation("IoT Hub Service started");
        return Task.CompletedTask;
    }

    public async Task SendUnlockCommandToDevice(string deviceId, bool shouldUnlock)
    {
        if (_serviceClient == null)
        {
            _logger.LogWarning("ServiceClient not initialized");
            return;
        }

        try
        {
            var commandMessage = new
            {
                command = shouldUnlock ? "unlock" : "deny",
                timestamp = DateTime.UtcNow
            };

            var messageString = JsonSerializer.Serialize(commandMessage);
            var message = new Message(Encoding.UTF8.GetBytes(messageString));

            await _serviceClient.SendAsync(deviceId, message);
            _logger.LogInformation($"Sent {(shouldUnlock ? "unlock" : "deny")} command to device {deviceId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error sending command to device {deviceId}");
        }
    }

    public async Task NotifyCameraOfDoorUnlock(string doorDeviceId, string? cardUid = null)
    {
        if (_serviceClient == null)
        {
            _logger.LogWarning("ServiceClient not initialized, cannot notify camera");
            return;
        }

        try
        {
            var cameraDeviceId = doorDeviceId.Replace("door", "camera");

            var commandMessage = new
            {
                command = "doorUnlocked",
                doorDeviceId = doorDeviceId,
                cardUid = cardUid ?? "",
                timestamp = DateTime.UtcNow
            };

            var messageString = JsonSerializer.Serialize(commandMessage);
            var message = new Message(Encoding.UTF8.GetBytes(messageString));

            await _serviceClient.SendAsync(cameraDeviceId, message);
            _logger.LogInformation(
                "Sent doorUnlocked notification to camera {CameraDevice} for door {DoorDevice}",
                cameraDeviceId, doorDeviceId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify camera for door {DoorDevice}", doorDeviceId);
        }
    }

    public async Task SendShowQrCommandToDevice(string deviceId, string unlockCode)
    {
        if (_serviceClient == null)
        {
            _logger.LogWarning("ServiceClient not initialized");
            return;
        }

        try
        {
            var commandMessage = new
            {
                command = "showQr",
                unlockCode = unlockCode,
                timestamp = DateTime.UtcNow
            };

            var messageString = JsonSerializer.Serialize(commandMessage);
            var message = new Message(Encoding.UTF8.GetBytes(messageString));

            await _serviceClient.SendAsync(deviceId, message);
            _logger.LogInformation($"Sent showQr command to device {deviceId} with code {unlockCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error sending showQr command to device {deviceId}");
        }
    }

    public async Task HandleCardScanD2C(string deviceId, string cardUid)
    {
        _logger.LogInformation($"Processing D2C card scan: DeviceId={deviceId}, CardUid={cardUid}");

        var response = await ProcessCardScan(deviceId, cardUid);

        if (response != null && response.Success && !string.IsNullOrEmpty(response.UnlockCode))
        {
            await SendShowQrCommandToDevice(deviceId, response.UnlockCode);
        }
        else
        {
            _logger.LogInformation($"Card scan denied or failed for device {deviceId}");
        }
    }

    public async Task<CardScanResponse?> ProcessCardScan(string deviceId, string cardUid)
    {
        using var scope = _serviceProvider.CreateScope();
        var keycardRepository = scope.ServiceProvider.GetRequiredService<IKeycardRepository>();
        var doorRepository = scope.ServiceProvider.GetRequiredService<IDoorRepository>();
        var unlockRequestRepository = scope.ServiceProvider.GetRequiredService<IUnlockRequestRepository>();
        var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();

        try
        {
            var keycard = await keycardRepository.GetActiveByCardUidWithUserAsync(cardUid);

            if (keycard == null)
            {
                _logger.LogWarning($"Unknown or inactive card: {cardUid}");
                await SendUnlockCommandToDevice(deviceId, false);
                return new CardScanResponse(false, "Card not recognized", null);
            }

            var door = await doorRepository.GetActiveByDeviceIdAsync(deviceId);

            if (door == null)
            {
                _logger.LogWarning($"Unknown or inactive door device: {deviceId}");
                await SendUnlockCommandToDevice(deviceId, false);
                return new CardScanResponse(false, "Door not found", null);
            }

            keycard.LastUsedAt = DateTime.UtcNow;

            var unlockRequest = new UnlockRequest
            {
                Id = Guid.NewGuid(),
                UserId = keycard.UserId,
                DoorId = door.Id,
                Challenge = Guid.NewGuid().ToString(),
                Status = UnlockRequestStatus.Pending,
                ExpiresAt = DateTime.UtcNow.AddSeconds(60),
                CreatedAt = DateTime.UtcNow,
                BleTriggered = false
            };

            await unlockRequestRepository.AddAsync(unlockRequest);
            await unlockRequestRepository.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync(
                "UnlockRequestReceived",
                keycard.User.DisplayName,
                door.DoorName,
                unlockRequest.Id.ToString());

            var shortCode = GenerateShortCode(6);
            var cacheKey = $"unlock_code_{shortCode}";
            cache.Set(cacheKey, unlockRequest.Id, TimeSpan.FromSeconds(120));

            _logger.LogInformation(
                $"Unlock request created: User={keycard.User.Username}, Door={door.DoorName}, RequestId={unlockRequest.Id}, Code={shortCode}");

            return new CardScanResponse(true, "Card scan processed", shortCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing card scan");
            await SendUnlockCommandToDevice(deviceId, false);
            return new CardScanResponse(false, $"Error: {ex.Message}", null);
        }
    }

    private async Task ListenForD2CMessages(string eventHubEndpoint, string eventHubName, CancellationToken ct)
    {
        try
        {
            await using var consumer = new EventHubConsumerClient(
                EventHubConsumerClient.DefaultConsumerGroupName,
                eventHubEndpoint,
                eventHubName);

            _logger.LogInformation("Connected to Event Hub for D2C messages");

            await foreach (PartitionEvent partitionEvent in consumer.ReadEventsAsync(
                startReadingAtEarliestEvent: false,
                new ReadEventOptions { MaximumWaitTime = TimeSpan.FromSeconds(30) },
                ct))
            {
                if (partitionEvent.Data == null) continue;

                try
                {
                    var body = partitionEvent.Data.EventBody.ToString();
                    _logger.LogDebug($"D2C message received: {body}");

                    var message = JsonSerializer.Deserialize<IoTHubMessage>(body, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (message == null || string.IsNullOrEmpty(message.MessageType))
                    {
                        _logger.LogDebug("Ignoring D2C message with no messageType");
                        continue;
                    }

                    var iotDeviceId = partitionEvent.Data.SystemProperties.ContainsKey("iothub-connection-device-id")
                        ? partitionEvent.Data.SystemProperties["iothub-connection-device-id"]?.ToString()
                        : null;

                    if (string.IsNullOrEmpty(iotDeviceId))
                    {
                        _logger.LogWarning("D2C message has no device ID in system properties");
                        continue;
                    }

                    if (message.MessageType == "cardScan")
                    {
                        if (string.IsNullOrEmpty(message.CardUid))
                        {
                            _logger.LogWarning($"cardScan D2C message from {iotDeviceId} has no cardUid");
                            continue;
                        }

                        await HandleCardScanD2C(iotDeviceId, message.CardUid);
                    }
                    else
                    {
                        _logger.LogDebug($"Ignoring D2C message with type: {message.MessageType}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing D2C message");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("D2C listener shutting down");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "D2C listener failed unexpectedly");
        }
    }

    private static string GenerateShortCode(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        var result = new char[length];
        for (int i = 0; i < length; i++)
            result[i] = chars[bytes[i] % chars.Length];
        return new string(result);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();

        if (_d2cListenerTask != null)
        {
            try
            {
                await _d2cListenerTask;
            }
            catch (OperationCanceledException) { }
        }

        _serviceClient?.Dispose();
        _logger.LogInformation("IoT Hub Service stopped");
    }
}
