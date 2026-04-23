namespace Project400API.Data;

public class AuditLog
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? DeviceId { get; set; }
    public string Result { get; set; } = string.Empty;
    public string? Details { get; set; }
}
