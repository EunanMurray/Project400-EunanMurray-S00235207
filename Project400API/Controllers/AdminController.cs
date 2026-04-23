using Microsoft.AspNetCore.Mvc;
using Project400API.Data;
using Project400API.Mappers;
using Project400API.Repositories.Interfaces;
using Project400.Shared.Models.Admin;

namespace Project400API.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IDoorRepository _doorRepository;
    private readonly IKeycardRepository _keycardRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public AdminController(
        IDoorRepository doorRepository,
        IKeycardRepository keycardRepository,
        IUserRepository userRepository,
        IAuditLogRepository auditLogRepository)
    {
        _doorRepository = doorRepository;
        _keycardRepository = keycardRepository;
        _userRepository = userRepository;
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet("doors")]
    public async Task<IActionResult> GetDoors()
    {
        var doors = await _doorRepository.GetAllAsync();
        return Ok(doors.Select(d => d.ToDto()));
    }

    [HttpPost("doors")]
    public async Task<IActionResult> CreateDoor([FromQuery] string doorName, [FromQuery] string deviceId)
    {
        var existingDoor = await _doorRepository.GetByDeviceIdAsync(deviceId);
        if (existingDoor != null)
        {
            return BadRequest(new { error = "Device already registered" });
        }

        var door = new Door
        {
            Id = Guid.NewGuid(),
            DoorName = doorName,
            DeviceId = deviceId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _doorRepository.AddAsync(door);
        await _doorRepository.SaveChangesAsync();

        return Ok(new { id = door.Id, doorName = door.DoorName, deviceId = door.DeviceId });
    }

    [HttpGet("keycards")]
    public async Task<IActionResult> GetKeycards()
    {
        var keycards = await _keycardRepository.GetAllWithUserAsync();
        return Ok(keycards.Select(k => k.ToDto()));
    }

    [HttpPost("keycards")]
    public async Task<IActionResult> CreateKeycard([FromQuery] string cardUid, [FromQuery] Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        var existingCard = await _keycardRepository.GetByCardUidAsync(cardUid);
        if (existingCard != null)
        {
            return BadRequest(new { error = "Card already registered" });
        }

        var keycard = new Keycard
        {
            Id = Guid.NewGuid(),
            CardUid = cardUid,
            UserId = userId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _keycardRepository.AddAsync(keycard);
        await _keycardRepository.SaveChangesAsync();

        return Ok(new { id = keycard.Id, cardUid = keycard.CardUid, userId = keycard.UserId });
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _userRepository.GetAllWithCredentialsAsync();
        return Ok(users.Select(u => u.ToDto()));
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] int count = 100)
    {
        var logs = await _auditLogRepository.GetAllAsync();
        var recent = logs.OrderByDescending(l => l.Timestamp).Take(count);
        return Ok(recent.Select(l => l.ToDto()));
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var doors = await _doorRepository.GetAllAsync();
        var users = await _userRepository.GetAllWithCredentialsAsync();
        var keycards = await _keycardRepository.GetAllWithUserAsync();

        return Ok(new
        {
            doorCount = doors.Count(),
            userCount = users.Count,
            keycardCount = keycards.Count()
        });
    }

    [HttpGet("users/check/{username}")]
    public async Task<IActionResult> CheckUserRegistration(string username)
    {
        var user = await _userRepository.GetByUsernameAsync(username);
        if (user == null)
            return Ok(new { registered = false });

        return Ok(new { registered = true, id = user.Id });
    }
}
