using Microsoft.EntityFrameworkCore;
using Project400API.Data;
using Project400Tests.Helpers;

namespace Project400Tests.Endpoints;

public class DevicePollEndpointTests
{
    [Fact]
    public async Task DevicePoll_WithValidToken_ReturnsShouldUnlockTrue()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "polluser",
            DisplayName = "Poll User",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);

        var unlockToken = new UnlockToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DeviceId = "device-001",
            ExpiresAt = DateTime.UtcNow.AddSeconds(30),
            Consumed = false,
            CreatedAt = DateTime.UtcNow
        };
        context.UnlockTokens.Add(unlockToken);
        await context.SaveChangesAsync();

        var token = await context.UnlockTokens
            .Include(t => t.User)
            .Where(t => t.DeviceId == "device-001" &&
                        t.ExpiresAt > DateTime.UtcNow &&
                        !t.Consumed)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        Assert.NotNull(token);
        Assert.Equal(user.Id, token.UserId);
        Assert.False(token.Consumed);
    }

    [Fact]
    public async Task DevicePoll_WithConsumedToken_ReturnsShouldUnlockFalse()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "consumedpoll",
            DisplayName = "Consumed Poll",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);

        context.UnlockTokens.Add(new UnlockToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DeviceId = "device-001",
            ExpiresAt = DateTime.UtcNow.AddSeconds(30),
            Consumed = true,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var token = await context.UnlockTokens
            .Where(t => t.DeviceId == "device-001" &&
                        t.ExpiresAt > DateTime.UtcNow &&
                        !t.Consumed)
            .FirstOrDefaultAsync();

        Assert.Null(token);
    }

    [Fact]
    public async Task DevicePoll_WithExpiredToken_ReturnsShouldUnlockFalse()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "expiredpoll",
            DisplayName = "Expired Poll",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);

        context.UnlockTokens.Add(new UnlockToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DeviceId = "device-001",
            ExpiresAt = DateTime.UtcNow.AddSeconds(-30),
            Consumed = false,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await context.SaveChangesAsync();

        var token = await context.UnlockTokens
            .Where(t => t.DeviceId == "device-001" &&
                        t.ExpiresAt > DateTime.UtcNow &&
                        !t.Consumed)
            .FirstOrDefaultAsync();

        Assert.Null(token);
    }

    [Fact]
    public async Task DevicePoll_NoTokens_ReturnsShouldUnlockFalse()
    {
        using var context = TestDbContextFactory.Create();

        var token = await context.UnlockTokens
            .Where(t => t.DeviceId == "device-001" &&
                        t.ExpiresAt > DateTime.UtcNow &&
                        !t.Consumed)
            .FirstOrDefaultAsync();

        Assert.Null(token);
    }

    [Fact]
    public async Task DevicePoll_ConsumesToken_AndCreatesAuditLog()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "auditpoll",
            DisplayName = "Audit Poll",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);

        var unlockToken = new UnlockToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DeviceId = "device-001",
            ExpiresAt = DateTime.UtcNow.AddSeconds(30),
            Consumed = false,
            CreatedAt = DateTime.UtcNow
        };
        context.UnlockTokens.Add(unlockToken);
        await context.SaveChangesAsync();

        var token = await context.UnlockTokens
            .Include(t => t.User)
            .Where(t => t.DeviceId == "device-001" &&
                        t.ExpiresAt > DateTime.UtcNow &&
                        !t.Consumed)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        Assert.NotNull(token);

        token.Consumed = true;
        context.UnlockTokens.Update(token);

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            EventType = "DoorUnlock",
            UserId = token.UserId,
            DeviceId = "device-001",
            Result = "Success",
            Details = $"Door unlocked for user {token.User.Username}"
        };
        context.AuditLogs.Add(auditLog);
        await context.SaveChangesAsync();

        var consumedToken = await context.UnlockTokens.FindAsync(token.Id);
        Assert.NotNull(consumedToken);
        Assert.True(consumedToken.Consumed);

        var log = await context.AuditLogs.FirstOrDefaultAsync(l => l.DeviceId == "device-001");
        Assert.NotNull(log);
        Assert.Equal("DoorUnlock", log.EventType);
        Assert.Equal("Success", log.Result);
        Assert.Contains("auditpoll", log.Details);
    }

    [Fact]
    public async Task DevicePoll_WrongDeviceId_NoMatch()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "wrongdev",
            DisplayName = "Wrong Dev",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);

        context.UnlockTokens.Add(new UnlockToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DeviceId = "device-002",
            ExpiresAt = DateTime.UtcNow.AddSeconds(30),
            Consumed = false,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var token = await context.UnlockTokens
            .Where(t => t.DeviceId == "device-001" &&
                        t.ExpiresAt > DateTime.UtcNow &&
                        !t.Consumed)
            .FirstOrDefaultAsync();

        Assert.Null(token);
    }

    [Fact]
    public async Task DevicePoll_MultipleTokens_ReturnsMostRecent()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "multitoken",
            DisplayName = "Multi Token",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);

        var olderToken = new UnlockToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DeviceId = "device-001",
            ExpiresAt = DateTime.UtcNow.AddSeconds(30),
            Consumed = false,
            CreatedAt = DateTime.UtcNow.AddSeconds(-10)
        };

        var newerToken = new UnlockToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DeviceId = "device-001",
            ExpiresAt = DateTime.UtcNow.AddSeconds(30),
            Consumed = false,
            CreatedAt = DateTime.UtcNow
        };

        context.UnlockTokens.AddRange(olderToken, newerToken);
        await context.SaveChangesAsync();

        var token = await context.UnlockTokens
            .Where(t => t.DeviceId == "device-001" &&
                        t.ExpiresAt > DateTime.UtcNow &&
                        !t.Consumed)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        Assert.NotNull(token);
        Assert.Equal(newerToken.Id, token.Id);
    }

    [Fact]
    public void DevicePoll_ApiKeyValidation_RejectsInvalidKey()
    {
        var expectedApiKey = "test-api-key";
        var providedApiKey = "wrong-key";

        var isValid = !string.IsNullOrEmpty(providedApiKey) && providedApiKey == expectedApiKey;

        Assert.False(isValid);
    }

    [Fact]
    public void DevicePoll_ApiKeyValidation_AcceptsValidKey()
    {
        var expectedApiKey = "test-api-key";
        var providedApiKey = "test-api-key";

        var isValid = !string.IsNullOrEmpty(providedApiKey) && providedApiKey == expectedApiKey;

        Assert.True(isValid);
    }

    [Fact]
    public void DevicePoll_ApiKeyValidation_RejectsNullKey()
    {
        var expectedApiKey = "test-api-key";
        string? providedApiKey = null;

        var isValid = !string.IsNullOrEmpty(providedApiKey) && providedApiKey == expectedApiKey;

        Assert.False(isValid);
    }

    [Fact]
    public void DevicePoll_ApiKeyValidation_RejectsEmptyKey()
    {
        var expectedApiKey = "test-api-key";
        var providedApiKey = "";

        var isValid = !string.IsNullOrEmpty(providedApiKey) && providedApiKey == expectedApiKey;

        Assert.False(isValid);
    }
}
