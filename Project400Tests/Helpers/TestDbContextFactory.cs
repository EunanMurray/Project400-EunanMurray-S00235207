using Microsoft.EntityFrameworkCore;
using Project400API.Data;

namespace Project400Tests.Helpers;

public static class TestDbContextFactory
{
    public static AppDbContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public static async Task<AppDbContext> CreateWithSeedDataAsync(string? databaseName = null)
    {
        var context = Create(databaseName);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            DisplayName = "Test User",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);

        var door = new Door
        {
            Id = Guid.NewGuid(),
            DoorName = "Main Entrance",
            DeviceId = "device-001",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Doors.Add(door);

        var keycard = new Keycard
        {
            Id = Guid.NewGuid(),
            CardUid = "CARD-ABC-123",
            UserId = user.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Keycards.Add(keycard);

        await context.SaveChangesAsync();
        return context;
    }
}
