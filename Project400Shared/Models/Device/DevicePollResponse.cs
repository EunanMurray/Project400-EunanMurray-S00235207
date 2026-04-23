namespace Project400.Shared.Models.Device;

public class DevicePollResponse
{
    public bool ShouldUnlock { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
