using Microsoft.EntityFrameworkCore;
using Project400API.Data;
using Project400Tests.Helpers;

namespace Project400Tests.Data;

public class UnlockTokenEntityTests
{
    [Fact]
    public async Task CreateUnlockToken_WithValidData_PersistsToDatabase()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "tokenuser",
            DisplayName = "Token User",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);

        var token = new UnlockToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DeviceId = "device-001",
            ExpiresAt = DateTime.UtcNow.AddSeconds(30),
            Consumed = false,
            CreatedAt = DateTime.UtcNow
        };
        context.UnlockTokens.Add(token);
        await context.SaveChangesAsync();

        var retrieved = await context.UnlockTokens.FindAsync(token.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("device-001", retrieved.DeviceId);
        Assert.False(retrieved.Consumed);
    }

    [Fact]
    public async Task UnlockToken_CanBeConsumed()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "consumeuser",
            DisplayName = "Consume User",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);

        var token = new UnlockToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DeviceId = "device-001",
            ExpiresAt = DateTime.UtcNow.AddSeconds(30),
            Consumed = false,
            CreatedAt = DateTime.UtcNow
        };
        context.UnlockTokens.Add(token);
        await context.SaveChangesAsync();

        token.Consumed = true;
        await context.SaveChangesAsync();

        var retrieved = await context.UnlockTokens.FindAsync(token.Id);
        Assert.NotNull(retrieved);
        Assert.True(retrieved.Consumed);
    }

    [Fact]
    public async Task UnlockToken_QueryActiveTokens_FiltersCorrectly()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "queryuser",
            DisplayName = "Query User",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);

        var activeToken = new UnlockToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DeviceId = "device-001",
            ExpiresAt = DateTime.UtcNow.AddSeconds(30),
            Consumed = false,
            CreatedAt = DateTime.UtcNow
        };

        var expiredToken = new UnlockToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DeviceId = "device-001",
            ExpiresAt = DateTime.UtcNow.AddSeconds(-30),
            Consumed = false,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        var consumedToken = new UnlockToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DeviceId = "device-001",
            ExpiresAt = DateTime.UtcNow.AddSeconds(30),
            Consumed = true,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        context.UnlockTokens.AddRange(activeToken, expiredToken, consumedToken);
        await context.SaveChangesAsync();

        var validTokens = await context.UnlockTokens
            .Where(t => t.DeviceId == "device-001" &&
                        t.ExpiresAt > DateTime.UtcNow &&
                        !t.Consumed)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        Assert.Single(validTokens);
        Assert.Equal(activeToken.Id, validTokens[0].Id);
    }

    [Fact]
    public async Task UnlockToken_UserNavigation_CanBeLoaded()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "navtokenuser",
            DisplayName = "Nav Token User",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);

        var token = new UnlockToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DeviceId = "device-001",
            ExpiresAt = DateTime.UtcNow.AddSeconds(30),
            Consumed = false,
            CreatedAt = DateTime.UtcNow
        };
        context.UnlockTokens.Add(token);
        await context.SaveChangesAsync();

        var retrieved = await context.UnlockTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == token.Id);

        Assert.NotNull(retrieved);
        Assert.NotNull(retrieved.User);
        Assert.Equal("navtokenuser", retrieved.User.Username);
    }
}
