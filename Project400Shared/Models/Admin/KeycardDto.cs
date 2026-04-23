namespace Project400.Shared.Models.Admin;

public class KeycardDto
{
    public Guid Id { get; set; }
    public string CardUid { get; set; } = "";
    public Guid UserId { get; set; }
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
