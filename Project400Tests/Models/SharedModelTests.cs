using Project400.Shared.Models.Auth;
using Project400.Shared.Models.Device;

namespace Project400Tests.Models;

public class SharedModelTests
{
    [Fact]
    public void DevicePollResponse_DefaultTimestamp_IsSetToUtcNow()
    {
        var before = DateTime.UtcNow;
        var response = new DevicePollResponse();
        var after = DateTime.UtcNow;

        Assert.True(response.Timestamp >= before);
        Assert.True(response.Timestamp <= after);
    }

    [Fact]
    public void DevicePollResponse_ShouldUnlock_DefaultsFalse()
    {
        var response = new DevicePollResponse();
        Assert.False(response.ShouldUnlock);
    }

    [Fact]
    public void DevicePollResponse_CanSetShouldUnlockTrue()
    {
        var response = new DevicePollResponse { ShouldUnlock = true };
        Assert.True(response.ShouldUnlock);
    }

    [Fact]
    public void CardScanResponse_Success_HasCorrectProperties()
    {
        var response = new CardScanResponse(true, "Card scan processed", "ABC123");

        Assert.True(response.Success);
        Assert.Equal("Card scan processed", response.Message);
        Assert.Equal("ABC123", response.UnlockCode);
    }

    [Fact]
    public void CardScanResponse_Failure_HasNullUnlockCode()
    {
        var response = new CardScanResponse(false, "Card not recognized", null);

        Assert.False(response.Success);
        Assert.Equal("Card not recognized", response.Message);
        Assert.Null(response.UnlockCode);
    }

    [Fact]
    public void CardScanResponse_DefaultUnlockCode_IsNull()
    {
        var response = new CardScanResponse(true, "Test");

        Assert.Null(response.UnlockCode);
    }

    [Fact]
    public void GenerateQRRequest_HasRequiredProperties()
    {
        var request = new GenerateQRRequest
        {
            Username = "testuser",
            DisplayName = "Test User"
        };

        Assert.Equal("testuser", request.Username);
        Assert.Equal("Test User", request.DisplayName);
    }

    [Fact]
    public void GenerateQRResponse_HasAllProperties()
    {
        var response = new GenerateQRResponse
        {
            RegistrationCode = "ABC123",
            QRCodeBase64 = "base64data",
            ExpiresInMinutes = 5
        };

        Assert.Equal("ABC123", response.RegistrationCode);
        Assert.Equal("base64data", response.QRCodeBase64);
        Assert.Equal(5, response.ExpiresInMinutes);
    }

    [Fact]
    public void ValidateQRResponse_ValidResponse()
    {
        var response = new ValidateQRResponse
        {
            Username = "validuser",
            DisplayName = "Valid User",
            Valid = true
        };

        Assert.True(response.Valid);
        Assert.Equal("validuser", response.Username);
    }

    [Fact]
    public void ValidateQRResponse_InvalidResponse()
    {
        var response = new ValidateQRResponse
        {
            Username = "",
            DisplayName = "",
            Valid = false
        };

        Assert.False(response.Valid);
    }
}
