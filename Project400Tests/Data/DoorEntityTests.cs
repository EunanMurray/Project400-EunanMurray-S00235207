using Project400API.Data;
using Project400Tests.Helpers;

namespace Project400Tests.Data;

public class DoorEntityTests
{
    [Fact]
    public async Task CreateDoor_WithValidData_PersistsToDatabase()
    {
        using var context = TestDbContextFactory.Create();

        var door = new Door
        {
            Id = Guid.NewGuid(),
            DoorName = "Lab Door",
            DeviceId = "esp32-lab-001",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Doors.Add(door);
        await context.SaveChangesAsync();

        var retrieved = await context.Doors.FindAsync(door.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Lab Door", retrieved.DoorName);
        Assert.Equal("esp32-lab-001", retrieved.DeviceId);
        Assert.True(retrieved.IsActive);
    }

    [Fact]
    public async Task CreateDoor_DefaultsIsActiveToTrue()
    {
        var door = new Door
        {
            Id = Guid.NewGuid(),
            DoorName = "Test Door",
            DeviceId = "test-device",
            CreatedAt = DateTime.UtcNow
        };

        Assert.True(door.IsActive);
    }

    [Fact]
    public async Task CreateDoor_CanSetInactive()
    {
        using var context = TestDbContextFactory.Create();

        var door = new Door
        {
            Id = Guid.NewGuid(),
            DoorName = "Disabled Door",
            DeviceId = "disabled-device",
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };

        context.Doors.Add(door);
        await context.SaveChangesAsync();

        var retrieved = await context.Doors.FindAsync(door.Id);
        Assert.NotNull(retrieved);
        Assert.False(retrieved.IsActive);
    }

    [Fact]
    public async Task MultipleDoors_CanBeRetrieved()
    {
        using var context = TestDbContextFactory.Create();

        var doors = new[]
        {
            new Door { Id = Guid.NewGuid(), DoorName = "Front Door", DeviceId = "dev-001", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Door { Id = Guid.NewGuid(), DoorName = "Back Door", DeviceId = "dev-002", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Door { Id = Guid.NewGuid(), DoorName = "Side Door", DeviceId = "dev-003", IsActive = false, CreatedAt = DateTime.UtcNow }
        };

        context.Doors.AddRange(doors);
        await context.SaveChangesAsync();

        var allDoors = context.Doors.ToList();
        Assert.Equal(3, allDoors.Count);

        var activeDoors = context.Doors.Where(d => d.IsActive).ToList();
        Assert.Equal(2, activeDoors.Count);
    }
}
