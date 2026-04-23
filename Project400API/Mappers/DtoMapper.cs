using Project400API.Data;
using Project400.Shared.Models.Admin;
using Project400.Shared.Models.Unlock;

namespace Project400API.Mappers;

public static class DtoMapper
{
    public static DoorDto ToDto(this Door door) => new()
    {
        Id = door.Id,
        DoorName = door.DoorName,
        DeviceId = door.DeviceId,
        IsActive = door.IsActive,
        CreatedAt = door.CreatedAt
    };

    public static KeycardDto ToDto(this Keycard keycard) => new()
    {
        Id = keycard.Id,
        CardUid = keycard.CardUid,
        UserId = keycard.UserId,
        Username = keycard.User?.Username ?? "",
        DisplayName = keycard.User?.DisplayName ?? "",
        IsActive = keycard.IsActive,
        CreatedAt = keycard.CreatedAt,
        LastUsedAt = keycard.LastUsedAt
    };

    public static UserDto ToDto(this User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        DisplayName = user.DisplayName,
        CredentialCount = user.Credentials?.Count ?? 0,
        CreatedAt = user.CreatedAt
    };

    public static AuditLogDto ToDto(this AuditLog log) => new()
    {
        Id = log.Id,
        Timestamp = log.Timestamp,
        EventType = log.EventType,
        UserId = log.UserId,
        DeviceId = log.DeviceId,
        Result = log.Result,
        Details = log.Details
    };

    public static UnlockRequestDto ToDto(this UnlockRequest request, string username) => new()
    {
        Id = request.Id,
        UserId = request.UserId,
        Username = username,
        DoorName = request.Door?.DoorName ?? "",
        Status = request.Status.ToString(),
        ExpiresAt = request.ExpiresAt,
        CreatedAt = request.CreatedAt
    };
}
