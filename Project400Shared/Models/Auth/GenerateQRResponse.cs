namespace Project400.Shared.Models.Auth;

public class GenerateQRResponse
{
    public required string RegistrationCode { get; set; }
    public required string QRCodeBase64 { get; set; }
    public int ExpiresInMinutes { get; set; }
}
