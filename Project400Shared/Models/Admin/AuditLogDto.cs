namespace Project400.Shared.Models.Admin;

public class AuditLogDto
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? DeviceId { get; set; }
    public string Result { get; set; } = string.Empty;
    public string? Details { get; set; }
}
