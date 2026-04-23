using Microsoft.AspNetCore.Mvc;
using Project400API.Data;
using Project400API.Repositories.Interfaces;
using Project400API.Services;
using Project400.Shared.Models.Device;

namespace Project400API.Controllers;

[ApiController]
[Route("api/device")]
public class DeviceController : ControllerBase
{
    private readonly IUnlockTokenRepository _unlockTokenRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IoTHubService _iotHubService;
    private readonly IConfiguration _configuration;

    public DeviceController(
        IUnlockTokenRepository unlockTokenRepository,
        IAuditLogRepository auditLogRepository,
        IoTHubService iotHubService,
        IConfiguration configuration)
    {
        _unlockTokenRepository = unlockTokenRepository;
        _auditLogRepository = auditLogRepository;
        _iotHubService = iotHubService;
        _configuration = configuration;
    }

    [HttpGet("poll")]
    public async Task<IActionResult> Poll([FromQuery] string? deviceId, [FromQuery] string? apiKey)
    {
        var expectedApiKey = _configuration["DeviceApiKey"] ?? "prod-api-key-change-this";
        if (string.IsNullOrEmpty(apiKey) || apiKey != expectedApiKey)
        {
            return Unauthorized();
        }

        var targetDeviceId = deviceId ?? "default";

        var unlockToken = await _unlockTokenRepository.GetActiveTokenForDeviceAsync(targetDeviceId);

        if (unlockToken != null)
        {
            unlockToken.Consumed = true;
            _unlockTokenRepository.Update(unlockToken);

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                EventType = "DoorUnlock",
                UserId = unlockToken.UserId,
                DeviceId = targetDeviceId,
                Result = "Success",
                Details = $"Door unlocked for user {unlockToken.User.Username}"
            };
            await _auditLogRepository.AddAsync(auditLog);

            await _unlockTokenRepository.SaveChangesAsync();

            _ = _iotHubService.NotifyCameraOfDoorUnlock(targetDeviceId);

            return Ok(new DevicePollResponse
            {
                ShouldUnlock = true,
                Timestamp = DateTime.UtcNow
            });
        }

        return Ok(new DevicePollResponse
        {
            ShouldUnlock = false,
            Timestamp = DateTime.UtcNow
        });
    }

    [HttpPost("card-scan")]
    public async Task<IActionResult> CardScan([FromQuery] string deviceId, [FromQuery] string cardUid)
    {
        var response = await _iotHubService.ProcessCardScan(deviceId, cardUid);
        if (response == null)
        {
            return BadRequest(new { error = "Failed to process card scan" });
        }
        return Ok(response);
    }
}
