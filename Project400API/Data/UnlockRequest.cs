namespace Project400API.Data;

public enum UnlockRequestStatus
{
    Pending,
    Approved,
    Denied,
    Expired
}

public class UnlockRequest
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid DoorId { get; set; }
    public string Challenge { get; set; } = string.Empty;
    public UnlockRequestStatus Status { get; set; } = UnlockRequestStatus.Pending;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public bool BleTriggered { get; set; }
    public DateTime? BleTriggerTime { get; set; }

    public User User { get; set; } = null!;
    public Door Door { get; set; } = null!;
}
