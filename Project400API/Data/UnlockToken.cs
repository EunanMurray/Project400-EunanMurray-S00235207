namespace Project400API.Data;

public class UnlockToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool Consumed { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
