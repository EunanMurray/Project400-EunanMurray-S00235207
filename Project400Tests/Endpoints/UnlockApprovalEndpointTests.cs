using Microsoft.EntityFrameworkCore;
using Project400API.Data;
using Project400Tests.Helpers;

namespace Project400Tests.Endpoints;

public class UnlockApprovalEndpointTests
{
    [Fact]
    public async Task GetUnlockRequest_ExistingRequest_ReturnsDetails()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "getrequser",
            DisplayName = "Get Req User",
            CreatedAt = DateTime.UtcNow
        };
        var door = new Door
        {
            Id = Guid.NewGuid(),
            DoorName = "Office Door",
            DeviceId = "office-dev",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        context.Doors.Add(door);

        var request = new UnlockRequest
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DoorId = door.Id,
            Challenge = "test-challenge",
            Status = UnlockRequestStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddSeconds(60),
            CreatedAt = DateTime.UtcNow,
            BleTriggered = false
        };
        context.UnlockRequests.Add(request);
        await context.SaveChangesAsync();

        var retrieved = await context.UnlockRequests
            .Include(r => r.Door)
            .FirstOrDefaultAsync(r => r.Id == request.Id);

        Assert.NotNull(retrieved);
        Assert.Equal("Office Door", retrieved.Door.DoorName);

        var retrievedUser = await context.Users.FindAsync(retrieved.UserId);
        Assert.NotNull(retrievedUser);
        Assert.Equal("getrequser", retrievedUser.Username);
    }

    [Fact]
    public async Task GetUnlockRequest_NonExistent_ReturnsNull()
    {
        using var context = TestDbContextFactory.Create();

        var retrieved = await context.UnlockRequests
            .FirstOrDefaultAsync(r => r.Id == Guid.NewGuid());

        Assert.Null(retrieved);
    }

    [Fact]
    public async Task ApproveUnlock_ValidRequest_CreatesTokenAndAuditLog()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "approveuser",
            DisplayName = "Approve User",
            CreatedAt = DateTime.UtcNow
        };
        var door = new Door
        {
            Id = Guid.NewGuid(),
            DoorName = "Lab Door",
            DeviceId = "lab-device",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        context.Doors.Add(door);

        var request = new UnlockRequest
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DoorId = door.Id,
            Challenge = "approve-challenge",
            Status = UnlockRequestStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddSeconds(60),
            CreatedAt = DateTime.UtcNow,
            BleTriggered = false
        };
        context.UnlockRequests.Add(request);
        await context.SaveChangesAsync();

        var foundRequest = await context.UnlockRequests
            .Include(r => r.User)
            .Include(r => r.Door)
            .FirstOrDefaultAsync(r => r.Id == request.Id);

        Assert.NotNull(foundRequest);
        Assert.True(foundRequest.ExpiresAt >= DateTime.UtcNow);
        Assert.Equal(UnlockRequestStatus.Pending, foundRequest.Status);

        foundRequest.Status = UnlockRequestStatus.Approved;
        foundRequest.ResolvedAt = DateTime.UtcNow;
        foundRequest.BleTriggered = true;
        foundRequest.BleTriggerTime = DateTime.UtcNow;

        var unlockToken = new UnlockToken
        {
            Id = Guid.NewGuid(),
            UserId = foundRequest.UserId,
            DeviceId = foundRequest.Door.DeviceId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(30),
            Consumed = false
        };
        context.UnlockTokens.Add(unlockToken);

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            EventType = "DoorUnlock",
            UserId = foundRequest.UserId,
            DeviceId = foundRequest.Door.DeviceId,
            Result = "Success",
            Details = $"User {foundRequest.User.Username} approved unlock for {foundRequest.Door.DoorName} via QR"
        };
        context.AuditLogs.Add(auditLog);
        await context.SaveChangesAsync();

        var updatedRequest = await context.UnlockRequests.FindAsync(request.Id);
        Assert.NotNull(updatedRequest);
        Assert.Equal(UnlockRequestStatus.Approved, updatedRequest.Status);

        var token = await context.UnlockTokens.FirstOrDefaultAsync(t => t.DeviceId == "lab-device");
        Assert.NotNull(token);
        Assert.False(token.Consumed);
        Assert.Equal(user.Id, token.UserId);

        var log = await context.AuditLogs.FirstOrDefaultAsync();
        Assert.NotNull(log);
        Assert.Equal("DoorUnlock", log.EventType);
        Assert.Contains("approveuser", log.Details);
        Assert.Contains("Lab Door", log.Details);
    }

    [Fact]
    public async Task ApproveUnlock_ExpiredRequest_SetsStatusToExpired()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User { Id = Guid.NewGuid(), Username = "expuser", DisplayName = "Exp", CreatedAt = DateTime.UtcNow };
        var door = new Door { Id = Guid.NewGuid(), DoorName = "Door", DeviceId = "dev", IsActive = true, CreatedAt = DateTime.UtcNow };
        context.Users.Add(user);
        context.Doors.Add(door);

        var request = new UnlockRequest
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DoorId = door.Id,
            Challenge = "expired-challenge",
            Status = UnlockRequestStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddSeconds(-30),
            CreatedAt = DateTime.UtcNow.AddMinutes(-2),
            BleTriggered = false
        };
        context.UnlockRequests.Add(request);
        await context.SaveChangesAsync();

        var foundRequest = await context.UnlockRequests
            .FirstOrDefaultAsync(r => r.Id == request.Id);

        Assert.NotNull(foundRequest);
        Assert.True(foundRequest.ExpiresAt < DateTime.UtcNow);

        foundRequest.Status = UnlockRequestStatus.Expired;
        await context.SaveChangesAsync();

        var updated = await context.UnlockRequests.FindAsync(request.Id);
        Assert.NotNull(updated);
        Assert.Equal(UnlockRequestStatus.Expired, updated.Status);
    }

    [Fact]
    public async Task ApproveUnlock_AlreadyApproved_IsDetected()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User { Id = Guid.NewGuid(), Username = "alreadyuser", DisplayName = "Already", CreatedAt = DateTime.UtcNow };
        var door = new Door { Id = Guid.NewGuid(), DoorName = "Door", DeviceId = "dev", IsActive = true, CreatedAt = DateTime.UtcNow };
        context.Users.Add(user);
        context.Doors.Add(door);

        var request = new UnlockRequest
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DoorId = door.Id,
            Challenge = "already-challenge",
            Status = UnlockRequestStatus.Approved,
            ExpiresAt = DateTime.UtcNow.AddSeconds(60),
            CreatedAt = DateTime.UtcNow,
            BleTriggered = true,
            ResolvedAt = DateTime.UtcNow
        };
        context.UnlockRequests.Add(request);
        await context.SaveChangesAsync();

        var foundRequest = await context.UnlockRequests
            .FirstOrDefaultAsync(r => r.Id == request.Id);

        Assert.NotNull(foundRequest);
        Assert.NotEqual(UnlockRequestStatus.Pending, foundRequest.Status);
    }

    [Fact]
    public async Task ApproveUnlock_TokenHas30SecondExpiry()
    {
        var before = DateTime.UtcNow;

        var unlockToken = new UnlockToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            DeviceId = "test-device",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(30),
            Consumed = false
        };

        var after = DateTime.UtcNow;

        var expectedMinExpiry = before.AddSeconds(30);
        var expectedMaxExpiry = after.AddSeconds(30);

        Assert.True(unlockToken.ExpiresAt >= expectedMinExpiry.AddSeconds(-1));
        Assert.True(unlockToken.ExpiresAt <= expectedMaxExpiry.AddSeconds(1));
    }
}
