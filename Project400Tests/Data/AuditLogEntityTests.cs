using Microsoft.EntityFrameworkCore;
using Project400API.Data;
using Project400Tests.Helpers;

namespace Project400Tests.Data;

public class AuditLogEntityTests
{
    [Fact]
    public async Task CreateAuditLog_WithValidData_PersistsToDatabase()
    {
        using var context = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();

        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            EventType = "DoorUnlock",
            UserId = userId,
            DeviceId = "device-001",
            Result = "Success",
            Details = "Door unlocked for test user"
        };

        context.AuditLogs.Add(log);
        await context.SaveChangesAsync();

        var retrieved = await context.AuditLogs.FindAsync(log.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("DoorUnlock", retrieved.EventType);
        Assert.Equal("Success", retrieved.Result);
        Assert.Equal(userId, retrieved.UserId);
    }

    [Fact]
    public async Task AuditLog_WithNullUserId_Persists()
    {
        using var context = TestDbContextFactory.Create();

        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            EventType = "SystemEvent",
            UserId = null,
            DeviceId = "device-001",
            Result = "Info",
            Details = "System startup"
        };

        context.AuditLogs.Add(log);
        await context.SaveChangesAsync();

        var retrieved = await context.AuditLogs.FindAsync(log.Id);
        Assert.NotNull(retrieved);
        Assert.Null(retrieved.UserId);
    }

    [Fact]
    public async Task AuditLog_WithNullDeviceId_Persists()
    {
        using var context = TestDbContextFactory.Create();

        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            EventType = "PasskeyRegistration",
            UserId = Guid.NewGuid(),
            DeviceId = null,
            Result = "Success",
            Details = "New passkey registered"
        };

        context.AuditLogs.Add(log);
        await context.SaveChangesAsync();

        var retrieved = await context.AuditLogs.FindAsync(log.Id);
        Assert.NotNull(retrieved);
        Assert.Null(retrieved.DeviceId);
    }

    [Fact]
    public async Task AuditLog_QueryByEventType_ReturnsCorrectLogs()
    {
        using var context = TestDbContextFactory.Create();

        var logs = new[]
        {
            new AuditLog { Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow, EventType = "DoorUnlock", Result = "Success" },
            new AuditLog { Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow, EventType = "PasskeyLogin", Result = "Success" },
            new AuditLog { Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow, EventType = "DoorUnlock", Result = "Failed" },
            new AuditLog { Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow, EventType = "PasskeyRegistration", Result = "Success" }
        };

        context.AuditLogs.AddRange(logs);
        await context.SaveChangesAsync();

        var doorUnlockLogs = await context.AuditLogs
            .Where(l => l.EventType == "DoorUnlock")
            .ToListAsync();

        Assert.Equal(2, doorUnlockLogs.Count);
    }

    [Fact]
    public async Task AuditLog_QueryByTimestampRange_FiltersCorrectly()
    {
        using var context = TestDbContextFactory.Create();

        var oldLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow.AddHours(-2),
            EventType = "OldEvent",
            Result = "Success"
        };

        var recentLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow.AddMinutes(-5),
            EventType = "RecentEvent",
            Result = "Success"
        };

        context.AuditLogs.AddRange(oldLog, recentLog);
        await context.SaveChangesAsync();

        var lastHour = await context.AuditLogs
            .Where(l => l.Timestamp > DateTime.UtcNow.AddHours(-1))
            .ToListAsync();

        Assert.Single(lastHour);
        Assert.Equal("RecentEvent", lastHour[0].EventType);
    }

    [Fact]
    public async Task AuditLog_QueryByUserId_ReturnsUserLogs()
    {
        using var context = TestDbContextFactory.Create();

        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();

        var logs = new[]
        {
            new AuditLog { Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow, EventType = "Login", UserId = userId1, Result = "Success" },
            new AuditLog { Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow, EventType = "Unlock", UserId = userId1, Result = "Success" },
            new AuditLog { Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow, EventType = "Login", UserId = userId2, Result = "Success" },
        };

        context.AuditLogs.AddRange(logs);
        await context.SaveChangesAsync();

        var user1Logs = await context.AuditLogs
            .Where(l => l.UserId == userId1)
            .ToListAsync();

        Assert.Equal(2, user1Logs.Count);
    }
}
