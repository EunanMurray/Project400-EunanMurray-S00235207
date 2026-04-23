using Microsoft.AspNetCore.Mvc;
using Project400API.Data;
using Project400API.Repositories.Interfaces;
using Project400API.Services;
using Project400.Shared.Models.Device;

namespace Project400API.Controllers;

[ApiController]
[Route("api/tailgate")]
public class TailgateController : ControllerBase
{
    private readonly TailgatingService _tailgatingService;
    private readonly ITailgateAlertRepository _alertRepository;
    private readonly IKeycardRepository _keycardRepository;
    private readonly IDoorRepository _doorRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TailgateController> _logger;

    public TailgateController(
        TailgatingService tailgatingService,
        ITailgateAlertRepository alertRepository,
        IKeycardRepository keycardRepository,
        IDoorRepository doorRepository,
        IConfiguration configuration,
        ILogger<TailgateController> logger)
    {
        _tailgatingService = tailgatingService;
        _alertRepository = alertRepository;
        _keycardRepository = keycardRepository;
        _doorRepository = doorRepository;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("analyze")]
    [RequestSizeLimit(2_000_000)]
    public async Task<IActionResult> AnalyzeFrame(
        [FromQuery] string cameraDeviceId,
        [FromQuery] string doorDeviceId,
        [FromQuery] string? lastCardUid,
        [FromQuery] string? apiKey)
    {
        var expectedApiKey = _configuration["CameraApiKey"] ?? "prod-camera-key-change-this";
        if (string.IsNullOrEmpty(apiKey) || apiKey != expectedApiKey)
        {
            return Unauthorized();
        }

        if (Request.ContentLength == null || Request.ContentLength == 0)
        {
            return BadRequest(new TailgateAnalysisResponse { Error = "No image data provided" });
        }

        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms);
        var imageData = ms.ToArray();

        if (imageData.Length < 100)
        {
            return BadRequest(new TailgateAnalysisResponse { Error = "Image data too small" });
        }

        _logger.LogInformation(
            "Received frame from camera {CameraId} for door {DoorId} ({Size} bytes), card: {CardUid}",
            cameraDeviceId, doorDeviceId, imageData.Length, lastCardUid ?? "none");

        Guid? userId = null;
        if (!string.IsNullOrEmpty(lastCardUid))
        {
            var keycard = await _keycardRepository.GetActiveByCardUidWithUserAsync(lastCardUid);
            userId = keycard?.UserId;
        }

        var result = await _tailgatingService.AnalyzeFrameAsync(
            cameraDeviceId, doorDeviceId, userId, imageData);

        var response = new TailgateAnalysisResponse
        {
            IsTailgating = result.IsTailgating,
            PeopleDetected = result.PeopleDetected,
            Confidence = result.Confidence,
            AlertId = result.AlertId,
            Error = result.Error
        };

        return Ok(response);
    }

    [HttpPost("test")]
    [RequestSizeLimit(2_000_000)]
    public async Task<IActionResult> TestAnalyze([FromQuery] string? apiKey)
    {
        var expectedApiKey = _configuration["CameraApiKey"] ?? "prod-camera-key-change-this";
        if (string.IsNullOrEmpty(apiKey) || apiKey != expectedApiKey)
        {
            return Unauthorized();
        }

        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms);
        var imageData = ms.ToArray();

        if (imageData.Length < 100)
        {
            return BadRequest(new { error = "No image data or image too small" });
        }

        _logger.LogInformation("Test analysis request: {Size} bytes", imageData.Length);

        var result = await _tailgatingService.TestAnalyzeAsync(imageData);

        return Ok(new
        {
            peopleDetected = result.PeopleDetected,
            averageConfidence = result.Confidence,
            isTailgating = result.IsTailgating,
            rawVisionResponse = result.AnalysisJson != null
                ? System.Text.Json.JsonSerializer.Deserialize<object>(result.AnalysisJson)
                : null,
            error = result.Error
        });
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAllAlerts([FromQuery] int count = 50)
    {
        var alerts = await _alertRepository.GetAllAlertsAsync(count);

        var dtos = alerts.Select(a => new TailgateAlertDto
        {
            Id = a.Id,
            UserName = a.User?.DisplayName,
            UserId = a.UserId,
            DeviceId = a.DeviceId,
            CameraDeviceId = a.CameraDeviceId,
            PeopleDetected = a.PeopleDetected,
            Confidence = a.Confidence,
            ImageBase64 = a.ImageData != null ? Convert.ToBase64String(a.ImageData) : null,
            Status = a.Status.ToString(),
            ReviewedBy = a.ReviewedBy,
            ReviewedAt = a.ReviewedAt,
            CreatedAt = a.CreatedAt
        }).ToList();

        return Ok(dtos);
    }

    [HttpGet("alerts/pending")]
    public async Task<IActionResult> GetPendingAlerts()
    {
        var alerts = await _alertRepository.GetPendingAlertsAsync();

        var dtos = alerts.Select(a => new TailgateAlertDto
        {
            Id = a.Id,
            UserName = a.User?.DisplayName,
            UserId = a.UserId,
            DeviceId = a.DeviceId,
            CameraDeviceId = a.CameraDeviceId,
            PeopleDetected = a.PeopleDetected,
            Confidence = a.Confidence,
            ImageBase64 = a.ImageData != null ? Convert.ToBase64String(a.ImageData) : null,
            Status = a.Status.ToString(),
            CreatedAt = a.CreatedAt
        }).ToList();

        return Ok(dtos);
    }

    [HttpPost("alerts/{alertId}/review")]
    public async Task<IActionResult> ReviewAlert(
        Guid alertId,
        [FromQuery] string status,
        [FromQuery] string reviewedBy)
    {
        var alert = await _alertRepository.GetByIdAsync(alertId);
        if (alert == null)
        {
            return NotFound();
        }

        if (Enum.TryParse<TailgateAlertStatus>(status, true, out var newStatus))
        {
            alert.Status = newStatus;
            alert.ReviewedBy = reviewedBy;
            alert.ReviewedAt = DateTime.UtcNow;
            _alertRepository.Update(alert);
            await _alertRepository.SaveChangesAsync();

            return Ok(new { success = true, status = alert.Status.ToString() });
        }

        return BadRequest(new { error = "Invalid status. Use: Reviewed, Dismissed, or Confirmed" });
    }
}
