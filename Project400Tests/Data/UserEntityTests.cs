using Project400API.Data;
using Project400Tests.Helpers;

namespace Project400Tests.Data;

public class UserEntityTests
{
    [Fact]
    public async Task CreateUser_WithValidData_PersistsToDatabase()
    {
        using var context = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Username = "eunan",
            DisplayName = "Eunan Murray",
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var retrieved = await context.Users.FindAsync(userId);
        Assert.NotNull(retrieved);
        Assert.Equal("eunan", retrieved.Username);
        Assert.Equal("Eunan Murray", retrieved.DisplayName);
    }

    [Fact]
    public async Task CreateUser_WithBleDeviceId_PersistsOptionalField()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "bleuser",
            DisplayName = "BLE User",
            BleDeviceId = "BLE-DEVICE-001",
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var retrieved = await context.Users.FindAsync(user.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("BLE-DEVICE-001", retrieved.BleDeviceId);
    }

    [Fact]
    public async Task CreateUser_WithoutBleDeviceId_DefaultsToNull()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "nobleuser",
            DisplayName = "No BLE User",
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var retrieved = await context.Users.FindAsync(user.Id);
        Assert.NotNull(retrieved);
        Assert.Null(retrieved.BleDeviceId);
    }

    [Fact]
    public async Task User_CredentialsNavigation_InitializesAsEmptyList()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "creduser",
            DisplayName = "Cred User",
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        Assert.NotNull(user.Credentials);
        Assert.Empty(user.Credentials);
    }

    [Fact]
    public async Task User_WithStoredCredentials_NavigationWorks()
    {
        using var context = TestDbContextFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "multcred",
            DisplayName = "Multi Cred",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);

        var credential = new StoredCredential
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CredentialId = new byte[] { 1, 2, 3, 4 },
            PublicKey = new byte[] { 5, 6, 7, 8 },
            UserHandle = new byte[] { 9, 10, 11, 12 },
            SignCount = 0,
            CredType = "public-key",
            AaGuid = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        context.StoredCredentials.Add(credential);

        await context.SaveChangesAsync();

        var retrievedUser = await context.Users
            .FindAsync(user.Id);

        var credentials = context.StoredCredentials
            .Where(c => c.UserId == user.Id)
            .ToList();

        Assert.Single(credentials);
        Assert.Equal(user.Id, credentials[0].UserId);
    }
}
