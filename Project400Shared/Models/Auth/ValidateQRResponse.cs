namespace Project400.Shared.Models.Auth;

public class ValidateQRResponse
{
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
    public bool Valid { get; set; }
}
