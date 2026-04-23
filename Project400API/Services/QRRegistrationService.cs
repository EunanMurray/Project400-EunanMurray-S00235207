using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using QRCoder;

namespace Project400API.Services;

public class QRRegistrationService
{
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<QRRegistrationService> _logger;

    public QRRegistrationService(
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<QRRegistrationService> logger)
    {
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public (string RegistrationCode, string QRCodeBase64) GenerateRegistrationQRCode(string username, string displayName)
    {
        var registrationCode = GenerateSecureCode();

        var registrationData = new RegistrationData
        {
            Username = username,
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow
        };

        var cacheKey = $"qr_registration_{registrationCode}";
        _cache.Set(cacheKey, registrationData, TimeSpan.FromMinutes(5));

        var baseUrl = _configuration["WebAppUrl"] ?? "https://www.eunanmurray.ie";
        if (baseUrl.Contains("azurecontainerapps.io"))
        {
            _logger.LogWarning("WebAppUrl is set to Azure Container Apps URL ({Url}), overriding to custom domain", baseUrl);
            baseUrl = "https://www.eunanmurray.ie";
        }
        var registrationUrl = $"{baseUrl}/mobile-register?code={registrationCode}";
        _logger.LogInformation("Registration QR URL: {Url}", registrationUrl);

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(registrationUrl, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeBytes = qrCode.GetGraphic(20);
        var qrCodeBase64 = Convert.ToBase64String(qrCodeBytes);

        _logger.LogInformation("Generated QR code for registration: {Code}", registrationCode);

        return (registrationCode, qrCodeBase64);
    }

    public RegistrationData? GetRegistrationData(string code)
    {
        var cacheKey = $"qr_registration_{code}";
        if (_cache.TryGetValue<RegistrationData>(cacheKey, out var data))
        {
            return data;
        }
        return null;
    }

    public void ConsumeRegistrationCode(string code)
    {
        var cacheKey = $"qr_registration_{code}";
        _cache.Remove(cacheKey);
    }

    private static string GenerateSecureCode()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .Substring(0, 16);
    }
}

public class RegistrationData
{
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
}
