using Microsoft.AspNetCore.Mvc;
using Project400API.Services;
using Project400.Shared.Models.Auth;

namespace Project400API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly PasskeyService _passkeyService;
    private readonly QRRegistrationService _qrService;

    public AuthController(PasskeyService passkeyService, QRRegistrationService qrService)
    {
        _passkeyService = passkeyService;
        _qrService = qrService;
    }

    [HttpPost("generate-qr")]
    public IActionResult GenerateQR([FromBody] GenerateQRRequest request)
    {
        try
        {
            var (code, qrCodeBase64) = _qrService.GenerateRegistrationQRCode(
                request.Username,
                request.DisplayName);

            return Ok(new GenerateQRResponse
            {
                RegistrationCode = code,
                QRCodeBase64 = qrCodeBase64,
                ExpiresInMinutes = 5
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("validate-qr/{code}")]
    public IActionResult ValidateQR(string code)
    {
        var data = _qrService.GetRegistrationData(code);
        if (data == null)
        {
            return NotFound(new { error = "Invalid or expired registration code" });
        }

        return Ok(new ValidateQRResponse
        {
            Username = data.Username,
            DisplayName = data.DisplayName,
            Valid = true
        });
    }

    [HttpPost("register-options")]
    public async Task<IActionResult> RegisterOptions([FromBody] RegisterOptionsRequest request)
    {
        try
        {
            var optionsJson = await _passkeyService.GenerateRegistrationOptionsAsync(
                request.Username,
                request.DisplayName);

            return Ok(new RegisterOptionsResponse
            {
                OptionsJson = optionsJson
            });
        }
        catch (Exception)
        {
            return BadRequest(new RegisterOptionsResponse
            {
                OptionsJson = string.Empty
            });
        }
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var (success, message) = await _passkeyService.CompleteRegistrationAsync(
                request.Username,
                request.AttestationResponse);

            return Ok(new RegisterResponse
            {
                Success = success,
                Message = message
            });
        }
        catch (Exception ex)
        {
            return Ok(new RegisterResponse
            {
                Success = false,
                Message = $"Registration failed: {ex.Message}"
            });
        }
    }

    [HttpPost("login-options")]
    public async Task<IActionResult> LoginOptions([FromBody] LoginOptionsRequest request)
    {
        try
        {
            var optionsJson = await _passkeyService.GenerateLoginOptionsAsync(request.Username);

            return Ok(new LoginOptionsResponse
            {
                OptionsJson = optionsJson
            });
        }
        catch (Exception)
        {
            return BadRequest(new LoginOptionsResponse
            {
                OptionsJson = string.Empty
            });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var (success, message, unlockTokenId) = await _passkeyService.CompleteLoginAsync(
                request.Username,
                request.AssertionResponse);

            return Ok(new LoginResponse
            {
                Success = success,
                Message = message,
                Token = unlockTokenId?.ToString() ?? string.Empty
            });
        }
        catch (Exception ex)
        {
            return Ok(new LoginResponse
            {
                Success = false,
                Message = $"Login failed: {ex.Message}",
                Token = string.Empty
            });
        }
    }
}
