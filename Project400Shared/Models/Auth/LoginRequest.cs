namespace Project400.Shared.Models.Auth;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string AssertionResponse { get; set; } = string.Empty;
}
