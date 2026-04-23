using Microsoft.EntityFrameworkCore;
using Project400API.Data;
using Project400Tests.Helpers;

namespace Project400Tests.Data;

public class UnlockRequestEntityTests
{
    [Fact]
    public async Task CreateUnlockRequest_WithValidData_PersistsToDatabase()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "requser",
            DisplayName = "Req User",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);

        var door = new Door
        {
            Id = Guid.NewGuid(),
            DoorName = "Main Door",
            DeviceId = "device-001",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Doors.Add(door);

        var request = new UnlockRequest
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DoorId = door.Id,
            Challenge = Guid.NewGuid().ToString(),
            Status = UnlockRequestStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddSeconds(60),
            CreatedAt = DateTime.UtcNow,
            BleTriggered = false
        };
        context.UnlockRequests.Add(request);
        await context.SaveChangesAsync();

        var retrieved = await context.UnlockRequests.FindAsync(request.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(UnlockRequestStatus.Pending, retrieved.Status);
        Assert.False(retrieved.BleTriggered);
        Assert.Null(retrieved.ResolvedAt);
    }

    [Fact]
    public async Task UnlockRequest_CanBeApproved()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "approveuser",
            DisplayName = "Approve User",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);

        var door = new Door
        {
            Id = Guid.NewGuid(),
            DoorName = "Lab Door",
            DeviceId = "device-lab",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Doors.Add(door);

        var request = new UnlockRequest
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DoorId = door.Id,
            Challenge = Guid.NewGuid().ToString(),
            Status = UnlockRequestStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddSeconds(60),
            CreatedAt = DateTime.UtcNow,
            BleTriggered = false
        };
        context.UnlockRequests.Add(request);
        await context.SaveChangesAsync();

        request.Status = UnlockRequestStatus.Approved;
        request.ResolvedAt = DateTime.UtcNow;
        request.BleTriggered = true;
        request.BleTriggerTime = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var retrieved = await context.UnlockRequests.FindAsync(request.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(UnlockRequestStatus.Approved, retrieved.Status);
        Assert.NotNull(retrieved.ResolvedAt);
        Assert.True(retrieved.BleTriggered);
        Assert.NotNull(retrieved.BleTriggerTime);
    }

    [Fact]
    public async Task UnlockRequest_CanBeDenied()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User { Id = Guid.NewGuid(), Username = "denyuser", DisplayName = "Deny", CreatedAt = DateTime.UtcNow };
        var door = new Door { Id = Guid.NewGuid(), DoorName = "Door", DeviceId = "dev", IsActive = true, CreatedAt = DateTime.UtcNow };
        context.Users.Add(user);
        context.Doors.Add(door);

        var request = new UnlockRequest
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DoorId = door.Id,
            Challenge = "challenge",
            Status = UnlockRequestStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddSeconds(60),
            CreatedAt = DateTime.UtcNow,
            BleTriggered = false
        };
        context.UnlockRequests.Add(request);
        await context.SaveChangesAsync();

        request.Status = UnlockRequestStatus.Denied;
        request.ResolvedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var retrieved = await context.UnlockRequests.FindAsync(request.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(UnlockRequestStatus.Denied, retrieved.Status);
    }

    [Fact]
    public async Task UnlockRequest_StatusEnum_HasCorrectValues()
    {
        Assert.Equal(0, (int)UnlockRequestStatus.Pending);
        Assert.Equal(1, (int)UnlockRequestStatus.Approved);
        Assert.Equal(2, (int)UnlockRequestStatus.Denied);
        Assert.Equal(3, (int)UnlockRequestStatus.Expired);
    }

    [Fact]
    public async Task UnlockRequest_DoorNavigation_CanBeLoaded()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User { Id = Guid.NewGuid(), Username = "navrequser", DisplayName = "Nav Req", CreatedAt = DateTime.UtcNow };
        var door = new Door { Id = Guid.NewGuid(), DoorName = "Server Room", DeviceId = "dev-srv", IsActive = true, CreatedAt = DateTime.UtcNow };
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
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == request.Id);

        Assert.NotNull(retrieved);
        Assert.NotNull(retrieved.Door);
        Assert.Equal("Server Room", retrieved.Door.DoorName);
        Assert.NotNull(retrieved.User);
        Assert.Equal("navrequser", retrieved.User.Username);
    }

    [Fact]
    public async Task UnlockRequest_QueryByStatusAndExpiry_FiltersCorrectly()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User { Id = Guid.NewGuid(), Username = "filteruser", DisplayName = "Filter", CreatedAt = DateTime.UtcNow };
        var door = new Door { Id = Guid.NewGuid(), DoorName = "Door", DeviceId = "dev", IsActive = true, CreatedAt = DateTime.UtcNow };
        context.Users.Add(user);
        context.Doors.Add(door);

        var pendingValid = new UnlockRequest
        {
            Id = Guid.NewGuid(), UserId = user.Id, DoorId = door.Id, Challenge = "c1",
            Status = UnlockRequestStatus.Pending, ExpiresAt = DateTime.UtcNow.AddSeconds(60),
            CreatedAt = DateTime.UtcNow, BleTriggered = false
        };

        var pendingExpired = new UnlockRequest
        {
            Id = Guid.NewGuid(), UserId = user.Id, DoorId = door.Id, Challenge = "c2",
            Status = UnlockRequestStatus.Pending, ExpiresAt = DateTime.UtcNow.AddSeconds(-60),
            CreatedAt = DateTime.UtcNow.AddMinutes(-2), BleTriggered = false
        };

        var approved = new UnlockRequest
        {
            Id = Guid.NewGuid(), UserId = user.Id, DoorId = door.Id, Challenge = "c3",
            Status = UnlockRequestStatus.Approved, ExpiresAt = DateTime.UtcNow.AddSeconds(60),
            CreatedAt = DateTime.UtcNow, BleTriggered = true
        };

        context.UnlockRequests.AddRange(pendingValid, pendingExpired, approved);
        await context.SaveChangesAsync();

        var pendingAndValid = await context.UnlockRequests
            .Where(r => r.Status == UnlockRequestStatus.Pending && r.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        Assert.Single(pendingAndValid);
        Assert.Equal(pendingValid.Id, pendingAndValid[0].Id);
    }
}
