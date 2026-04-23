using Microsoft.AspNetCore.SignalR;
using Moq;
using Project400API.Hubs;

namespace Project400Tests.Hubs;

public class UnlockHubTests
{
    private UnlockHub CreateHub(out Mock<IHubCallerClients> clientsMock, out Mock<HubCallerContext> contextMock)
    {
        clientsMock = new Mock<IHubCallerClients>();
        contextMock = new Mock<HubCallerContext>();

        var callerMock = new Mock<ISingleClientProxy>();
        callerMock
            .Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        clientsMock.Setup(c => c.Caller).Returns(callerMock.Object);
        clientsMock.Setup(c => c.All).Returns(callerMock.Object);
        clientsMock.Setup(c => c.Client(It.IsAny<string>())).Returns(callerMock.Object);

        contextMock.Setup(c => c.ConnectionId).Returns("test-connection-id");

        var hub = new UnlockHub
        {
            Clients = clientsMock.Object,
            Context = contextMock.Object
        };

        return hub;
    }

    [Fact]
    public async Task RegisterUser_SendsRegisteredConfirmation()
    {
        var hub = CreateHub(out var clientsMock, out _);

        await hub.RegisterUser("user-123");

        clientsMock.Verify(c => c.Caller, Times.Once);
    }

    [Fact]
    public async Task RegisterDevice_SendsDeviceRegisteredConfirmation()
    {
        var hub = CreateHub(out var clientsMock, out _);

        await hub.RegisterDevice("device-001");

        clientsMock.Verify(c => c.Caller, Times.Once);
    }

    [Fact]
    public async Task NotifyUnlockStatus_SendsToAllClients()
    {
        var hub = CreateHub(out var clientsMock, out _);

        await hub.NotifyUnlockStatus("Approved", "Main Door");

        clientsMock.Verify(c => c.All, Times.Once);
    }

    [Fact]
    public async Task RegisterUser_DoesNotThrow()
    {
        var hub = CreateHub(out _, out _);

        var exception = await Record.ExceptionAsync(() => hub.RegisterUser("user-456"));
        Assert.Null(exception);
    }

    [Fact]
    public async Task RegisterDevice_DoesNotThrow()
    {
        var hub = CreateHub(out _, out _);

        var exception = await Record.ExceptionAsync(() => hub.RegisterDevice("device-002"));
        Assert.Null(exception);
    }

    [Fact]
    public async Task NotifyUnlockStatus_WithDifferentStatuses_DoesNotThrow()
    {
        var hub = CreateHub(out _, out _);

        var statuses = new[] { "Approved", "Denied", "Expired", "Pending" };

        foreach (var status in statuses)
        {
            var exception = await Record.ExceptionAsync(() =>
                hub.NotifyUnlockStatus(status, "Test Door"));
            Assert.Null(exception);
        }
    }

    [Fact]
    public async Task OnDisconnectedAsync_DoesNotThrow()
    {
        var hub = CreateHub(out _, out _);

        var exception = await Record.ExceptionAsync(() =>
            hub.OnDisconnectedAsync(null));

        Assert.Null(exception);
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithException_DoesNotThrow()
    {
        var hub = CreateHub(out _, out _);

        var exception = await Record.ExceptionAsync(() =>
            hub.OnDisconnectedAsync(new Exception("Connection lost")));

        Assert.Null(exception);
    }
}
