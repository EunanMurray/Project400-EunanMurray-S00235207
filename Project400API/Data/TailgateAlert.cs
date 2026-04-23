namespace Project400API.Data;

public enum TailgateAlertStatus
{
    Pending,
    Reviewed,
    Dismissed,
    Confirmed
}

public class TailgateAlert
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string CameraDeviceId { get; set; } = string.Empty;
    public int PeopleDetected { get; set; }
    public double Confidence { get; set; }
    public string? ImageUrl { get; set; }
    public byte[]? ImageData { get; set; }
    public string? AnalysisJson { get; set; }
    public TailgateAlertStatus Status { get; set; } = TailgateAlertStatus.Pending;
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
}
