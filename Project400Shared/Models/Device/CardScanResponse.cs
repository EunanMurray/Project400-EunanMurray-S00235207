namespace Project400.Shared.Models.Device;

public record CardScanResponse(
    bool Success,
    string Message,
    string? UnlockCode = null
);
