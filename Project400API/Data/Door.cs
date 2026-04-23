namespace Project400API.Data;

public class Door
{
    public Guid Id { get; set; }
    public string DoorName { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
