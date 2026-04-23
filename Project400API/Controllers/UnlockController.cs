using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Project400API.Data;
using Project400API.Mappers;
using Project400API.Repositories.Interfaces;
using Project400API.Services;
using Project400.Shared.Models.Unlock;

namespace Project400API.Controllers;

[ApiController]
[Route("api/unlock")]
public class UnlockController : ControllerBase
{
    private readonly IUnlockRequestRepository _unlockRequestRepository;
    private readonly IUnlockTokenRepository _unlockTokenRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUserRepository _userRepository;
    private readonly IoTHubService _iotHubService;
    private readonly IMemoryCache _cache;

    public UnlockController(
        IUnlockRequestRepository unlockRequestRepository,
        IUnlockTokenRepository unlockTokenRepository,
        IAuditLogRepository auditLogRepository,
        IUserRepository userRepository,
        IoTHubService iotHubService,
        IMemoryCache cache)
    {
        _unlockRequestRepository = unlockRequestRepository;
        _unlockTokenRepository = unlockTokenRepository;
        _auditLogRepository = auditLogRepository;
        _userRepository = userRepository;
        _iotHubService = iotHubService;
        _cache = cache;
    }

    [HttpGet("request/{id:guid}")]
    public async Task<IActionResult> GetRequest(Guid id)
    {
        var request = await _unlockRequestRepository.GetWithDoorAsync(id);
        if (request == null)
        {
            return NotFound(new { error = "Unlock request not found" });
        }

        var user = await _userRepository.GetByIdAsync(request.UserId);

        return Ok(request.ToDto(user?.Username ?? ""));
    }

    [HttpPost("approve")]
    public async Task<IActionResult> Approve([FromQuery] Guid requestId)
    {
        var request = await _unlockRequestRepository.GetWithUserAndDoorAsync(requestId);

        if (request == null)
        {
            return NotFound(new { error = "Unlock request not found" });
        }

        if (request.ExpiresAt < DateTime.UtcNow)
        {
            request.Status = UnlockRequestStatus.Expired;
            await _unlockRequestRepository.SaveChangesAsync();
            return BadRequest(new { error = "Unlock request expired" });
        }

        if (request.Status != UnlockRequestStatus.Pending)
        {
            return BadRequest(new { error = "Unlock request already processed" });
        }

        request.Status = UnlockRequestStatus.Approved;
        request.ResolvedAt = DateTime.UtcNow;
        request.BleTriggered = true;
        request.BleTriggerTime = DateTime.UtcNow;

        var unlockToken = new UnlockToken
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            DeviceId = request.Door.DeviceId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(30),
            Consumed = false
        };
        await _unlockTokenRepository.AddAsync(unlockToken);

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            EventType = "DoorUnlock",
            UserId = request.UserId,
            DeviceId = request.Door.DeviceId,
            Result = "Success",
            Details = $"User {request.User.Username} approved unlock for {request.Door.DoorName} via QR"
        };
        await _auditLogRepository.AddAsync(auditLog);

        await _unlockRequestRepository.SaveChangesAsync();

        await _iotHubService.SendUnlockCommandToDevice(request.Door.DeviceId, true);

        _ = _iotHubService.NotifyCameraOfDoorUnlock(request.Door.DeviceId);

        return Ok(new UnlockApproveResponse { Success = true, Message = "Door unlocked" });
    }

    [HttpGet("by-code/{code}")]
    public IActionResult GetByCode(string code)
    {
        var cacheKey = $"unlock_code_{code.ToUpperInvariant()}";
        if (_cache.TryGetValue<Guid>(cacheKey, out var requestId))
        {
            return Ok(new UnlockByCodeResponse { RequestId = requestId });
        }
        return NotFound(new { error = "Invalid or expired unlock code" });
    }
}
