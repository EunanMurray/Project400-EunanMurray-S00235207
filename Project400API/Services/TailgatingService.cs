using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Project400API.Data;
using Project400API.Hubs;
using Project400API.Repositories.Interfaces;

namespace Project400API.Services;

public class TailgatingService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<UnlockHub> _hubContext;
    private readonly ILogger<TailgatingService> _logger;

    private const int TailgateThreshold = 2;
    private const double MinPersonConfidence = 0.5;

    public TailgatingService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider,
        IHubContext<UnlockHub> hubContext,
        ILogger<TailgatingService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<TailgateAnalysisResult> AnalyzeFrameAsync(
        string cameraDeviceId,
        string doorDeviceId,
        Guid? userId,
        byte[] imageData)
    {
        var result = new TailgateAnalysisResult();

        var enabled = _configuration.GetValue("Tailgating:Enabled", true);
        if (!enabled)
        {
            _logger.LogInformation("Tailgating detection disabled by config");
            return result;
        }

        try
        {
            var visionResult = await CallAzureVisionAsync(imageData);

            if (visionResult == null)
            {
                _logger.LogWarning("Azure Vision returned null for camera {CameraId}", cameraDeviceId);
                result.Error = "Vision API returned no result";
                return result;
            }

            result.PeopleDetected = visionResult.PeopleCount;
            result.Confidence = visionResult.AverageConfidence;
            result.AnalysisJson = visionResult.RawJson;
            result.IsTailgating = visionResult.PeopleCount >= TailgateThreshold;

            _logger.LogInformation(
                "Vision analysis for camera {CameraId}: {PeopleCount} people detected (tailgating: {IsTailgating})",
                cameraDeviceId, visionResult.PeopleCount, result.IsTailgating);

            if (result.IsTailgating)
            {
                var alertId = await CreateTailgateAlertAsync(cameraDeviceId, doorDeviceId, userId, result, imageData);
                result.AlertId = alertId.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing frame from camera {CameraId}", cameraDeviceId);
            result.Error = ex.Message;
        }

        return result;
    }

    public async Task<TailgateAnalysisResult> TestAnalyzeAsync(byte[] imageData)
    {
        var result = new TailgateAnalysisResult();

        try
        {
            var visionResult = await CallAzureVisionAsync(imageData);

            if (visionResult == null)
            {
                result.Error = "Vision API returned no result";
                return result;
            }

            result.PeopleDetected = visionResult.PeopleCount;
            result.Confidence = visionResult.AverageConfidence;
            result.AnalysisJson = visionResult.RawJson;
            result.IsTailgating = visionResult.PeopleCount >= TailgateThreshold;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in test analysis");
            result.Error = ex.Message;
        }

        return result;
    }

    private async Task<VisionAnalysisResult?> CallAzureVisionAsync(byte[] imageData)
    {
        var endpoint = _configuration["Azure:Computer:Vision:Endpoint"];
        var apiKey = _configuration["Azure:Computer:Vision:Key1"];

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("Azure Computer Vision endpoint or API key not configured");
            throw new InvalidOperationException("Azure Computer Vision is not configured. Set Azure--Computer--Vision--Endpoint and Azure--Computer--Vision--Key1 in Key Vault.");
        }

        var client = _httpClientFactory.CreateClient("AzureVision");

        var requestUrl = $"{endpoint.TrimEnd('/')}/computervision/imageanalysis:analyze?api-version=2024-02-01&features=people";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        request.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
        request.Content = new ByteArrayContent(imageData);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Azure Vision API error {StatusCode}: {Body}", response.StatusCode, responseBody);
            throw new HttpRequestException($"Azure Vision API returned {response.StatusCode}: {responseBody}");
        }

        _logger.LogDebug("Azure Vision raw response: {Response}", responseBody);

        return ParseVisionResponse(responseBody);
    }

    private VisionAnalysisResult? ParseVisionResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var result = new VisionAnalysisResult
            {
                RawJson = json
            };

            if (root.TryGetProperty("peopleResult", out var peopleResult) &&
                peopleResult.TryGetProperty("values", out var people))
            {
                var confidencePeople = new List<double>();

                foreach (var person in people.EnumerateArray())
                {
                    var confidence = person.GetProperty("confidence").GetDouble();
                    if (confidence >= MinPersonConfidence)
                    {
                        confidencePeople.Add(confidence);
                    }
                }

                result.PeopleCount = confidencePeople.Count;
                result.AverageConfidence = confidencePeople.Count > 0
                    ? confidencePeople.Average()
                    : 0;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Azure Vision response");
            return null;
        }
    }

    public async Task<Guid> CreateTailgateAlertAsync(
        string cameraDeviceId,
        string doorDeviceId,
        Guid? userId,
        TailgateAnalysisResult analysis,
        byte[]? imageData = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var alertRepo = scope.ServiceProvider.GetRequiredService<ITailgateAlertRepository>();
        var auditRepo = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();

        var alert = new TailgateAlert
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceId = doorDeviceId,
            CameraDeviceId = cameraDeviceId,
            PeopleDetected = analysis.PeopleDetected,
            Confidence = analysis.Confidence,
            ImageData = imageData,
            AnalysisJson = analysis.AnalysisJson,
            Status = TailgateAlertStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await alertRepo.AddAsync(alert);

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            EventType = "TailgateDetected",
            UserId = userId,
            DeviceId = doorDeviceId,
            Result = "Alert",
            Details = $"Tailgating detected: {analysis.PeopleDetected} people at camera {cameraDeviceId}. Confidence: {analysis.Confidence:F2}"
        };
        await auditRepo.AddAsync(auditLog);

        await alertRepo.SaveChangesAsync();

        string userName = "Unknown";
        if (userId.HasValue)
        {
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var user = await userRepo.GetByIdAsync(userId.Value);
            userName = user?.DisplayName ?? "Unknown";
        }

        await _hubContext.Clients.Group("Admins").SendAsync("TailgateAlertRaised", new
        {
            alertId = alert.Id,
            doorDeviceId,
            cameraDeviceId,
            peopleDetected = analysis.PeopleDetected,
            confidence = analysis.Confidence,
            userName,
            userId,
            timestamp = alert.CreatedAt
        });

        _logger.LogWarning(
            "TAILGATE ALERT: {PeopleCount} people detected at door {DoorDevice} (camera {CameraDevice}), user who scanned: {User}",
            analysis.PeopleDetected, doorDeviceId, cameraDeviceId, userName);

        return alert.Id;
    }
}

public class TailgateAnalysisResult
{
    public int PeopleDetected { get; set; }
    public double Confidence { get; set; }
    public bool IsTailgating { get; set; }
    public string? AlertId { get; set; }
    public string? AnalysisJson { get; set; }
    public string? Error { get; set; }
}

internal class VisionAnalysisResult
{
    public int PeopleCount { get; set; }
    public double AverageConfidence { get; set; }
    public string RawJson { get; set; } = string.Empty;
}
