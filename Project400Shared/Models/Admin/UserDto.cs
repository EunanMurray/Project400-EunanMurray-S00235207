namespace Project400.Shared.Models.Admin;

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int CredentialCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
