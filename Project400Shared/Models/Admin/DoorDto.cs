namespace Project400.Shared.Models.Admin;

public class DoorDto
{
    public Guid Id { get; set; }
    public string DoorName { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
