using Microsoft.EntityFrameworkCore;
using Project400API.Data;
using Project400Tests.Helpers;

namespace Project400Tests.Data;

public class KeycardEntityTests
{
    [Fact]
    public async Task CreateKeycard_WithValidData_PersistsToDatabase()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "carduser",
            DisplayName = "Card User",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);

        var keycard = new Keycard
        {
            Id = Guid.NewGuid(),
            CardUid = "RFID-001-ABC",
            UserId = user.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Keycards.Add(keycard);
        await context.SaveChangesAsync();

        var retrieved = await context.Keycards.FindAsync(keycard.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("RFID-001-ABC", retrieved.CardUid);
        Assert.Equal(user.Id, retrieved.UserId);
        Assert.True(retrieved.IsActive);
    }

    [Fact]
    public async Task Keycard_DefaultsIsActiveToTrue()
    {
        var keycard = new Keycard
        {
            Id = Guid.NewGuid(),
            CardUid = "TEST",
            UserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        Assert.True(keycard.IsActive);
    }

    [Fact]
    public async Task Keycard_LastUsedAt_DefaultsToNull()
    {
        var keycard = new Keycard
        {
            Id = Guid.NewGuid(),
            CardUid = "TEST",
            UserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        Assert.Null(keycard.LastUsedAt);
    }

    [Fact]
    public async Task Keycard_CanUpdateLastUsedAt()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "updateuser",
            DisplayName = "Update User",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);

        var keycard = new Keycard
        {
            Id = Guid.NewGuid(),
            CardUid = "UPDATE-CARD",
            UserId = user.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Keycards.Add(keycard);
        await context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        keycard.LastUsedAt = now;
        await context.SaveChangesAsync();

        var retrieved = await context.Keycards.FindAsync(keycard.Id);
        Assert.NotNull(retrieved);
        Assert.NotNull(retrieved.LastUsedAt);
        Assert.Equal(now, retrieved.LastUsedAt);
    }

    [Fact]
    public async Task Keycard_UserNavigation_CanBeLoaded()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "navuser",
            DisplayName = "Nav User",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);

        var keycard = new Keycard
        {
            Id = Guid.NewGuid(),
            CardUid = "NAV-CARD",
            UserId = user.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Keycards.Add(keycard);
        await context.SaveChangesAsync();

        var retrieved = await context.Keycards
            .Include(k => k.User)
            .FirstOrDefaultAsync(k => k.Id == keycard.Id);

        Assert.NotNull(retrieved);
        Assert.NotNull(retrieved.User);
        Assert.Equal("navuser", retrieved.User.Username);
    }
}
