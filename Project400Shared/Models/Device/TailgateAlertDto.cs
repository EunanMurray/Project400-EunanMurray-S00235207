namespace Project400.Shared.Models.Device;

public class TailgateAlertDto
{
    public Guid Id { get; set; }
    public string? UserName { get; set; }
    public Guid? UserId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string CameraDeviceId { get; set; } = string.Empty;
    public int PeopleDetected { get; set; }
    public double Confidence { get; set; }
    public string? ImageBase64 { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
