namespace Project400.Shared.Models.Auth;

public class GenerateQRRequest
{
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
}
