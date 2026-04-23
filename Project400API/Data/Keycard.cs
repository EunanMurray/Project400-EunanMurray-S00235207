namespace Project400API.Data;

public class Keycard
{
    public Guid Id { get; set; }
    public string CardUid { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }

    public User User { get; set; } = null!;
}
