using Microsoft.EntityFrameworkCore;
using Project400API.Data;
using Project400Tests.Helpers;

namespace Project400Tests.Endpoints;

public class AdminEndpointTests
{
    [Fact]
    public async Task RegisterKeycard_WithValidUser_CreatesKeycard()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "keycarduser",
            DisplayName = "Keycard User",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var foundUser = await context.Users.FindAsync(user.Id);
        Assert.NotNull(foundUser);

        var existingCard = await context.Keycards
            .FirstOrDefaultAsync(k => k.CardUid == "NEW-CARD-001");
        Assert.Null(existingCard);

        var keycard = new Keycard
        {
            Id = Guid.NewGuid(),
            CardUid = "NEW-CARD-001",
            UserId = user.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Keycards.Add(keycard);
        await context.SaveChangesAsync();

        var saved = await context.Keycards.FindAsync(keycard.Id);
        Assert.NotNull(saved);
        Assert.Equal("NEW-CARD-001", saved.CardUid);
        Assert.Equal(user.Id, saved.UserId);
    }

    [Fact]
    public async Task RegisterKeycard_DuplicateCard_Detected()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "dupuser",
            DisplayName = "Dup User",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);

        var existingCard = new Keycard
        {
            Id = Guid.NewGuid(),
            CardUid = "DUP-CARD",
            UserId = user.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Keycards.Add(existingCard);
        await context.SaveChangesAsync();

        var duplicate = await context.Keycards
            .FirstOrDefaultAsync(k => k.CardUid == "DUP-CARD");

        Assert.NotNull(duplicate);
    }

    [Fact]
    public async Task RegisterKeycard_NonExistentUser_ReturnsNull()
    {
        using var context = TestDbContextFactory.Create();

        var nonExistentUserId = Guid.NewGuid();
        var user = await context.Users.FindAsync(nonExistentUserId);

        Assert.Null(user);
    }

    [Fact]
    public async Task RegisterDoor_WithValidData_CreatesDoor()
    {
        using var context = TestDbContextFactory.Create();

        var existingDoor = await context.Doors
            .FirstOrDefaultAsync(d => d.DeviceId == "new-esp32-device");
        Assert.Null(existingDoor);

        var door = new Door
        {
            Id = Guid.NewGuid(),
            DoorName = "Conference Room",
            DeviceId = "new-esp32-device",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Doors.Add(door);
        await context.SaveChangesAsync();

        var saved = await context.Doors.FindAsync(door.Id);
        Assert.NotNull(saved);
        Assert.Equal("Conference Room", saved.DoorName);
        Assert.Equal("new-esp32-device", saved.DeviceId);
    }

    [Fact]
    public async Task RegisterDoor_DuplicateDevice_Detected()
    {
        using var context = TestDbContextFactory.Create();

        var existingDoor = new Door
        {
            Id = Guid.NewGuid(),
            DoorName = "Existing Door",
            DeviceId = "duplicate-device",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Doors.Add(existingDoor);
        await context.SaveChangesAsync();

        var duplicate = await context.Doors
            .FirstOrDefaultAsync(d => d.DeviceId == "duplicate-device");

        Assert.NotNull(duplicate);
    }

    [Fact]
    public async Task GetKeycards_ReturnsAllWithUserInfo()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "listuser",
            DisplayName = "List User",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);

        var cards = new[]
        {
            new Keycard { Id = Guid.NewGuid(), CardUid = "CARD-A", UserId = user.Id, IsActive = true, CreatedAt = DateTime.UtcNow },
            new Keycard { Id = Guid.NewGuid(), CardUid = "CARD-B", UserId = user.Id, IsActive = true, CreatedAt = DateTime.UtcNow }
        };
        context.Keycards.AddRange(cards);
        await context.SaveChangesAsync();

        var keycards = await context.Keycards
            .Include(k => k.User)
            .Select(k => new
            {
                k.Id,
                k.CardUid,
                k.UserId,
                Username = k.User.Username,
                DisplayName = k.User.DisplayName,
                k.IsActive,
                k.CreatedAt,
                k.LastUsedAt
            })
            .ToListAsync();

        Assert.Equal(2, keycards.Count);
        Assert.All(keycards, k => Assert.Equal("listuser", k.Username));
    }

    [Fact]
    public async Task GetDoors_ReturnsAllDoors()
    {
        using var context = TestDbContextFactory.Create();

        context.Doors.AddRange(
            new Door { Id = Guid.NewGuid(), DoorName = "Door A", DeviceId = "dev-a", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Door { Id = Guid.NewGuid(), DoorName = "Door B", DeviceId = "dev-b", IsActive = false, CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var doors = await context.Doors
            .Select(d => new { d.Id, d.DoorName, d.DeviceId, d.IsActive, d.CreatedAt })
            .ToListAsync();

        Assert.Equal(2, doors.Count);
    }

    [Fact]
    public async Task GetUsers_ReturnsWithCredentialCount()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "credcountuser",
            DisplayName = "Cred Count",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);

        context.StoredCredentials.AddRange(
            new StoredCredential { Id = Guid.NewGuid(), UserId = user.Id, CredentialId = new byte[] { 1 }, PublicKey = new byte[] { 2 }, UserHandle = new byte[] { 3 }, SignCount = 0, CredType = "public-key", AaGuid = Guid.NewGuid(), CreatedAt = DateTime.UtcNow },
            new StoredCredential { Id = Guid.NewGuid(), UserId = user.Id, CredentialId = new byte[] { 4 }, PublicKey = new byte[] { 5 }, UserHandle = new byte[] { 6 }, SignCount = 0, CredType = "public-key", AaGuid = Guid.NewGuid(), CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var users = await context.Users
            .Include(u => u.Credentials)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.DisplayName,
                CredentialCount = u.Credentials.Count,
                u.CreatedAt
            })
            .ToListAsync();

        Assert.Single(users);
        Assert.Equal(2, users[0].CredentialCount);
    }
}
