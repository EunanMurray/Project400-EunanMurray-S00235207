namespace Project400.Shared.Models.Unlock;

public class UnlockStatus
{
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedAt { get; set; }
    public string UnlockedBy { get; set; } = string.Empty;
}
