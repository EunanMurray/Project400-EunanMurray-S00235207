using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.Extensions.Caching.Memory;
using Project400API.Data;
using Project400API.Repositories.Interfaces;

namespace Project400API.Services;

public class PasskeyService
{
    private readonly IFido2 _fido2;
    private readonly IUserRepository _userRepository;
    private readonly IStoredCredentialRepository _credentialRepository;
    private readonly IUnlockTokenRepository _unlockTokenRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PasskeyService> _logger;

    public PasskeyService(
        IFido2 fido2,
        IUserRepository userRepository,
        IStoredCredentialRepository credentialRepository,
        IUnlockTokenRepository unlockTokenRepository,
        IAuditLogRepository auditLogRepository,
        IMemoryCache cache,
        ILogger<PasskeyService> logger)
    {
        _fido2 = fido2;
        _userRepository = userRepository;
        _credentialRepository = credentialRepository;
        _unlockTokenRepository = unlockTokenRepository;
        _auditLogRepository = auditLogRepository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> GenerateRegistrationOptionsAsync(string username, string displayName, bool allowCrossPlatform = true)
    {
        try
        {
            var existingUser = await _userRepository.GetByUsernameAsync(username);

            Fido2User user;
            if (existingUser != null)
            {
                user = new Fido2User
                {
                    Name = existingUser.Username,
                    DisplayName = existingUser.DisplayName,
                    Id = existingUser.Id.ToByteArray()
                };
            }
            else
            {
                var userId = Guid.NewGuid();
                user = new Fido2User
                {
                    Name = username,
                    DisplayName = displayName,
                    Id = userId.ToByteArray()
                };
            }

            var existingCredentials = (await _credentialRepository.GetByUserIdAsync(new Guid(user.Id)))
                .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
                .ToList();

            var authenticatorSelection = new AuthenticatorSelection
            {
                RequireResidentKey = false,
                UserVerification = UserVerificationRequirement.Preferred,
                AuthenticatorAttachment = allowCrossPlatform ? null : AuthenticatorAttachment.Platform
            };

            var options = _fido2.RequestNewCredential(
                user,
                existingCredentials,
                authenticatorSelection,
                AttestationConveyancePreference.None);

            var cacheKey = $"registration_{username}";
            _cache.Set(cacheKey, options.ToJson(), TimeSpan.FromMinutes(2));

            return options.ToJson();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating registration options for {Username}", username);
            throw;
        }
    }

    public async Task<(bool Success, string Message)> CompleteRegistrationAsync(
        string username,
        string attestationResponseJson)
    {
        try
        {
            var cacheKey = $"registration_{username}";
            if (!_cache.TryGetValue<string>(cacheKey, out var optionsJson) || string.IsNullOrEmpty(optionsJson))
            {
                return (false, "Registration session expired. Please try again.");
            }

            var options = CredentialCreateOptions.FromJson(optionsJson);
            var attestationResponse = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(
                attestationResponseJson);

            if (attestationResponse == null)
            {
                return (false, "Invalid attestation response format.");
            }

            var success = await _fido2.MakeNewCredentialAsync(
                attestationResponse,
                options,
                async (args, cancellationToken) =>
                {
                    var exists = await _credentialRepository.ExistsByCredentialIdAsync(args.CredentialId);
                    return !exists;
                });

            if (success.Result == null)
            {
                return (false, "Credential verification failed.");
            }

            var userId = new Guid(options.User.Id);
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                user = new User
                {
                    Id = userId,
                    Username = options.User.Name,
                    DisplayName = options.User.DisplayName,
                    CreatedAt = DateTime.UtcNow
                };
                await _userRepository.AddAsync(user);
            }

            var credential = new StoredCredential
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CredentialId = success.Result.CredentialId,
                PublicKey = success.Result.PublicKey,
                UserHandle = success.Result.User.Id,
                SignCount = success.Result.Counter,
                CredType = success.Result.CredType,
                AaGuid = success.Result.Aaguid,
                CreatedAt = DateTime.UtcNow
            };

            await _credentialRepository.AddAsync(credential);
            await _credentialRepository.SaveChangesAsync();

            await LogAuditEventAsync("PasskeyRegistration", user.Id, null, "Success",
                $"Registered new passkey for user {username}");

            _cache.Remove(cacheKey);

            return (true, "Passkey registered successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing registration for {Username}", username);
            return (false, $"Registration failed: {ex.Message}");
        }
    }

    public async Task<string> GenerateLoginOptionsAsync(string username)
    {
        try
        {
            var user = await _userRepository.GetWithCredentialsAsync(username);

            if (user == null || !user.Credentials.Any())
            {
                throw new InvalidOperationException("User not found or has no registered passkeys.");
            }

            var allowedCredentials = user.Credentials
                .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
                .ToList();

            var options = _fido2.GetAssertionOptions(
                allowedCredentials,
                UserVerificationRequirement.Preferred);

            var cacheKey = $"login_{username}";
            _cache.Set(cacheKey, options.ToJson(), TimeSpan.FromMinutes(2));

            return options.ToJson();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating login options for {Username}", username);
            throw;
        }
    }

    public async Task<(bool Success, string Message, Guid? UnlockTokenId)> CompleteLoginAsync(
        string username,
        string assertionResponseJson)
    {
        try
        {
            var cacheKey = $"login_{username}";
            if (!_cache.TryGetValue<string>(cacheKey, out var optionsJson) || string.IsNullOrEmpty(optionsJson))
            {
                return (false, "Login session expired. Please try again.", null);
            }

            var options = AssertionOptions.FromJson(optionsJson);
            var assertionResponse = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(
                assertionResponseJson);

            if (assertionResponse == null)
            {
                return (false, "Invalid assertion response format.", null);
            }

            var credential = await _credentialRepository.GetWithUserByCredentialIdAsync(assertionResponse.Id);

            if (credential == null)
            {
                return (false, "Credential not found.", null);
            }

            var success = await _fido2.MakeAssertionAsync(
                assertionResponse,
                options,
                credential.PublicKey,
                credential.SignCount,
                async (args, cancellationToken) =>
                {
                    var cred = await _credentialRepository.GetByUserHandleAndCredentialIdAsync(
                        args.UserHandle, args.CredentialId);
                    return cred != null;
                });

            if (success.Status != "ok")
            {
                await LogAuditEventAsync("PasskeyLogin", credential.UserId, null, "Failed",
                    $"Failed login attempt for {username}: {success.ErrorMessage}");
                return (false, "Authentication failed.", null);
            }

            credential.SignCount = success.Counter;
            _credentialRepository.Update(credential);

            var unlockToken = new UnlockToken
            {
                Id = Guid.NewGuid(),
                UserId = credential.UserId,
                DeviceId = "default",
                ExpiresAt = DateTime.UtcNow.AddSeconds(30),
                Consumed = false,
                CreatedAt = DateTime.UtcNow
            };

            await _unlockTokenRepository.AddAsync(unlockToken);
            await _unlockTokenRepository.SaveChangesAsync();

            await LogAuditEventAsync("PasskeyLogin", credential.UserId, "default", "Success",
                $"Successful login for user {username}, unlock token created");

            _cache.Remove(cacheKey);

            return (true, "Authentication successful! Door will unlock for 30 seconds.", unlockToken.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing login for {Username}", username);
            return (false, $"Authentication failed: {ex.Message}", null);
        }
    }

    private async Task LogAuditEventAsync(
        string eventType,
        Guid? userId,
        string? deviceId,
        string result,
        string? details)
    {
        try
        {
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                EventType = eventType,
                UserId = userId,
                DeviceId = deviceId,
                Result = result,
                Details = details
            };

            await _auditLogRepository.AddAsync(auditLog);
            await _auditLogRepository.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log audit event");
        }
    }
}
