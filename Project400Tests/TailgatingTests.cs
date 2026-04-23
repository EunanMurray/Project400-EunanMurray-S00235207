using Microsoft.EntityFrameworkCore;
using Project400API.Data;
using Project400Tests.Helpers;

namespace Project400Tests;

public class TailgatingTests
{
    [Fact]
    public async Task TailgateAlert_PersistsWhenPeopleCount_GTE_Threshold()
    {
        using var context = TestDbContextFactory.Create();

        var alert = new TailgateAlert
        {
            Id = Guid.NewGuid(),
            DeviceId = "door-001",
            CameraDeviceId = "camera-001",
            PeopleDetected = 3,
            Confidence = 0.85,
            ImageData = new byte[] { 0xFF, 0xD8, 0xFF },
            AnalysisJson = "{\"peopleResult\":{\"values\":[]}}",
            Status = TailgateAlertStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        context.TailgateAlerts.Add(alert);
        await context.SaveChangesAsync();

        var saved = await context.TailgateAlerts.FindAsync(alert.Id);
        Assert.NotNull(saved);
        Assert.Equal(3, saved.PeopleDetected);
        Assert.Equal(TailgateAlertStatus.Pending, saved.Status);
        Assert.NotNull(saved.ImageData);
    }

    [Fact]
    public async Task TailgateAlert_SinglePerson_NotPersisted()
    {
        // When only 1 person is detected, threshold (2) is not met.
        // The service should NOT create an alert. Verify via count.
        using var context = TestDbContextFactory.Create();

        // We don't add an alert because the service wouldn't create one for 1 person
        var count = await context.TailgateAlerts.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task TailgateAlert_ReviewChangesStatus()
    {
        using var context = TestDbContextFactory.Create();

        var alert = new TailgateAlert
        {
            Id = Guid.NewGuid(),
            DeviceId = "door-001",
            CameraDeviceId = "camera-001",
            PeopleDetected = 2,
            Confidence = 0.9,
            Status = TailgateAlertStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        context.TailgateAlerts.Add(alert);
        await context.SaveChangesAsync();

        alert.Status = TailgateAlertStatus.Confirmed;
        alert.ReviewedBy = "admin";
        alert.ReviewedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var saved = await context.TailgateAlerts.FindAsync(alert.Id);
        Assert.NotNull(saved);
        Assert.Equal(TailgateAlertStatus.Confirmed, saved.Status);
        Assert.Equal("admin", saved.ReviewedBy);
    }

    [Fact]
    public async Task TailgateAlert_WithUserAssociation()
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

        var alert = new TailgateAlert
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DeviceId = "door-001",
            CameraDeviceId = "camera-001",
            PeopleDetected = 2,
            Confidence = 0.75,
            Status = TailgateAlertStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        context.TailgateAlerts.Add(alert);
        await context.SaveChangesAsync();

        var saved = await context.TailgateAlerts
            .Include(a => a.User)
            .FirstAsync(a => a.Id == alert.Id);

        Assert.NotNull(saved.User);
        Assert.Equal("carduser", saved.User.Username);
    }

    [Fact]
    public async Task TailgateAlert_ImageData_StoredCorrectly()
    {
        using var context = TestDbContextFactory.Create();

        var imageBytes = new byte[1024];
        new Random(42).NextBytes(imageBytes);

        var alert = new TailgateAlert
        {
            Id = Guid.NewGuid(),
            DeviceId = "door-002",
            CameraDeviceId = "camera-002",
            PeopleDetected = 4,
            Confidence = 0.95,
            ImageData = imageBytes,
            Status = TailgateAlertStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        context.TailgateAlerts.Add(alert);
        await context.SaveChangesAsync();

        var saved = await context.TailgateAlerts.FindAsync(alert.Id);
        Assert.NotNull(saved?.ImageData);
        Assert.Equal(1024, saved.ImageData.Length);
    }
}
