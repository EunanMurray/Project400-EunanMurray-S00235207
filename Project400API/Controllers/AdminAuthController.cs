using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Project400API.Services;
using Project400API.Repositories.Interfaces;
using QRCoder;

namespace Project400API.Controllers;

[ApiController]
[Route("api/auth/admin-login")]
public class AdminAuthController : ControllerBase
{
    private readonly IMemoryCache _cache;
    private readonly PasskeyService _passkeyService;
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminAuthController> _logger;

    public AdminAuthController(
        IMemoryCache cache,
        PasskeyService passkeyService,
        IUserRepository userRepository,
        IConfiguration configuration,
        ILogger<AdminAuthController> logger)
    {
        _cache = cache;
        _passkeyService = passkeyService;
        _userRepository = userRepository;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("start")]
    public IActionResult Start()
    {
        var code = GenerateShortCode(8);
        var cacheKey = $"admin_login_{code}";

        var entry = new AdminLoginEntry
        {
            Code = code,
            Status = "pending",
            UserId = null,
            CreatedAt = DateTime.UtcNow
        };

        _cache.Set(cacheKey, entry, TimeSpan.FromMinutes(5));

        var webBaseUrl = _configuration["WebAppUrl"] ?? "https://www.eunanmurray.ie";
        if (webBaseUrl.Contains("azurecontainerapps.io"))
        {
            webBaseUrl = "https://www.eunanmurray.ie";
        }
        var qrUrl = $"{webBaseUrl}/admin-claim/{code}";

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeBytes = qrCode.GetGraphic(20);
        var qrImageBase64 = Convert.ToBase64String(qrCodeBytes);

        _logger.LogInformation("Admin login started with code {Code}", code);

        return Ok(new
        {
            code,
            qrUrl,
            qrImageBase64
        });
    }

    [HttpPost("claim/{code}")]
    public async Task<IActionResult> Claim(string code, [FromBody] AdminClaimRequest request)
    {
        var cacheKey = $"admin_login_{code}";

        if (!_cache.TryGetValue<AdminLoginEntry>(cacheKey, out var entry) || entry == null)
        {
            return StatusCode(410, new { error = "Code expired or invalid" });
        }

        if (entry.Status != "pending")
        {
            return BadRequest(new { error = "Code already used" });
        }

        try
        {
            var (success, message, _) = await _passkeyService.CompleteLoginAsync(
                request.Username, request.AssertionResponse);

            if (!success)
            {
                return BadRequest(new { error = message });
            }

            var user = await _userRepository.GetByUsernameAsync(request.Username);
            if (user == null || !user.IsAdmin)
            {
                return StatusCode(403, new { error = "You are not an admin" });
            }

            entry.Status = "claimed";
            entry.UserId = user.Id;
            _cache.Set(cacheKey, entry, TimeSpan.FromMinutes(5));

            _logger.LogInformation("Admin login claimed by user {Username} ({UserId})", user.Username, user.Id);

            return Ok(new { status = "claimed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin login claim failed");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("direct")]
    public async Task<IActionResult> Direct([FromBody] AdminClaimRequest request)
    {
        try
        {
            var (success, message, _) = await _passkeyService.CompleteLoginAsync(
                request.Username, request.AssertionResponse);

            if (!success)
            {
                return BadRequest(new { error = message });
            }

            var user = await _userRepository.GetByUsernameAsync(request.Username);
            if (user == null || !user.IsAdmin)
            {
                return StatusCode(403, new { error = "You are not an admin" });
            }

            _logger.LogInformation("Admin direct login by user {Username} ({UserId})", user.Username, user.Id);

            return Ok(new
            {
                userId = user.Id,
                displayName = user.DisplayName,
                username = user.Username
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin direct login failed");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("check/{code}")]
    public async Task<IActionResult> Check(string code)
    {
        var cacheKey = $"admin_login_{code}";

        if (!_cache.TryGetValue<AdminLoginEntry>(cacheKey, out var entry) || entry == null)
        {
            return Ok(new { status = "expired" });
        }

        if (entry.Status == "claimed" && entry.UserId.HasValue)
        {
            var user = await _userRepository.GetByIdAsync(entry.UserId.Value);
            if (user != null)
            {
                entry.Status = "consumed";
                _cache.Set(cacheKey, entry, TimeSpan.FromMinutes(1));

                _logger.LogInformation("Admin login consumed for user {Username}", user.Username);

                return Ok(new
                {
                    status = "consumed",
                    userId = user.Id,
                    displayName = user.DisplayName,
                    username = user.Username
                });
            }
        }

        return Ok(new { status = entry.Status });
    }

    private static string GenerateShortCode(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        var result = new char[length];
        for (int i = 0; i < length; i++)
            result[i] = chars[bytes[i] % chars.Length];
        return new string(result);
    }
}

public class AdminLoginEntry
{
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public Guid? UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminClaimRequest
{
    public string Username { get; set; } = string.Empty;
    public string AssertionResponse { get; set; } = string.Empty;
}
