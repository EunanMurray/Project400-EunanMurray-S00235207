using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Project400API.Services;

namespace Project400Tests.Services;

public class QRRegistrationServiceTests
{
    private readonly QRRegistrationService _service;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<QRRegistrationService>> _loggerMock;

    public QRRegistrationServiceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<QRRegistrationService>>();

        var configValues = new Dictionary<string, string?>
        {
            { "WebAppUrl", "https://localhost:7076" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        _service = new QRRegistrationService(_cache, configuration, _loggerMock.Object);
    }

    [Fact]
    public void GenerateRegistrationQRCode_ReturnsRegistrationCode()
    {
        var (code, qrCodeBase64) = _service.GenerateRegistrationQRCode("testuser", "Test User");

        Assert.NotNull(code);
        Assert.NotEmpty(code);
        Assert.Equal(16, code.Length);
    }

    [Fact]
    public void GenerateRegistrationQRCode_ReturnsValidBase64QRCode()
    {
        var (code, qrCodeBase64) = _service.GenerateRegistrationQRCode("testuser", "Test User");

        Assert.NotNull(qrCodeBase64);
        Assert.NotEmpty(qrCodeBase64);

        var bytes = Convert.FromBase64String(qrCodeBase64);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public void GenerateRegistrationQRCode_CachesRegistrationData()
    {
        var (code, _) = _service.GenerateRegistrationQRCode("cacheuser", "Cache User");

        var data = _service.GetRegistrationData(code);
        Assert.NotNull(data);
        Assert.Equal("cacheuser", data.Username);
        Assert.Equal("Cache User", data.DisplayName);
    }

    [Fact]
    public void GenerateRegistrationQRCode_GeneratesUniqueCodesEachTime()
    {
        var (code1, _) = _service.GenerateRegistrationQRCode("user1", "User 1");
        var (code2, _) = _service.GenerateRegistrationQRCode("user2", "User 2");

        Assert.NotEqual(code1, code2);
    }

    [Fact]
    public void GenerateRegistrationQRCode_CodeDoesNotContainSpecialChars()
    {
        var (code, _) = _service.GenerateRegistrationQRCode("testuser", "Test User");

        Assert.DoesNotContain("+", code);
        Assert.DoesNotContain("/", code);
        Assert.DoesNotContain("=", code);
    }

    [Fact]
    public void GetRegistrationData_WithValidCode_ReturnsData()
    {
        var (code, _) = _service.GenerateRegistrationQRCode("validuser", "Valid User");

        var data = _service.GetRegistrationData(code);

        Assert.NotNull(data);
        Assert.Equal("validuser", data.Username);
        Assert.Equal("Valid User", data.DisplayName);
    }

    [Fact]
    public void GetRegistrationData_WithInvalidCode_ReturnsNull()
    {
        var data = _service.GetRegistrationData("nonexistent-code");

        Assert.Null(data);
    }

    [Fact]
    public void GetRegistrationData_SetsCreatedAtTimestamp()
    {
        var before = DateTime.UtcNow;
        var (code, _) = _service.GenerateRegistrationQRCode("timeuser", "Time User");
        var after = DateTime.UtcNow;

        var data = _service.GetRegistrationData(code);

        Assert.NotNull(data);
        Assert.True(data.CreatedAt >= before);
        Assert.True(data.CreatedAt <= after);
    }

    [Fact]
    public void ConsumeRegistrationCode_RemovesFromCache()
    {
        var (code, _) = _service.GenerateRegistrationQRCode("consumeuser", "Consume User");

        var dataBefore = _service.GetRegistrationData(code);
        Assert.NotNull(dataBefore);

        _service.ConsumeRegistrationCode(code);

        var dataAfter = _service.GetRegistrationData(code);
        Assert.Null(dataAfter);
    }

    [Fact]
    public void ConsumeRegistrationCode_WithInvalidCode_DoesNotThrow()
    {
        var exception = Record.Exception(() => _service.ConsumeRegistrationCode("invalid-code"));
        Assert.Null(exception);
    }

    [Fact]
    public void RegistrationData_HasRequiredProperties()
    {
        var data = new RegistrationData
        {
            Username = "propuser",
            DisplayName = "Prop User",
            CreatedAt = DateTime.UtcNow
        };

        Assert.Equal("propuser", data.Username);
        Assert.Equal("Prop User", data.DisplayName);
        Assert.True(data.CreatedAt <= DateTime.UtcNow);
    }
}
