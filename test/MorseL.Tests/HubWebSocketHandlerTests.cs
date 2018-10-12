using System;
using System.Threading.Tasks;
using Moq;
using SocketConnection = MorseL.Sockets.Connection;
using Xunit;
using MorseL.Shared.Tests;

namespace MorseL.Tests
{
    public class HubWebSocketHandlerTests
    {
        public class DefaultHub : Hub<IClientInvoker>
        { }

        public class FailHub : Hub<IClientInvoker>
        {
            public override Task OnConnectedAsync(SocketConnection connection)
            {
                throw new NotSupportedException();
            }

            public override Task OnDisconnectedAsync(Exception exception)
            {
                throw new NotSupportedException();
            }
        }

        private readonly ServicesMocker Mocker = new ServicesMocker();

        public HubWebSocketHandlerTests()
        {
            Mocker.RegisterHub<DefaultHub>();
            Mocker.RegisterHub<FailHub>();
        }

        [Fact]
        public void Construction_ShouldNotThrow()
        {
            var sut = new HubWebSocketHandler<DefaultHub>(Mocker.ServiceProviderMock.Object, Mocker.LoggerFactoryMock.Object);
        }

        [Fact]
        public async Task OnConnectedAsync_ShouldInvokeHubCreateAndRelease()
        {
            var sut = new HubWebSocketHandler<DefaultHub>(Mocker.ServiceProviderMock.Object, Mocker.LoggerFactoryMock.Object);

            var connection = new SocketConnection(Guid.NewGuid().ToString(), Mocker.ChannelMock.Object);

            await sut.OnConnectedAsync(connection);

            var defaultHubActivatorMock = Mocker.GetHubActivator<DefaultHub>();
            defaultHubActivatorMock.Verify(m => m.Create(), Times.Once);
            defaultHubActivatorMock.Verify(m => m.Release(It.IsAny<DefaultHub>()), Times.Once);
        }

        [Fact]
        public async Task OnConnectedAsync_ReleaseShouldBeInvoked_IfHubOnConnectedAsyncThrows()
        {
            var sut = new HubWebSocketHandler<FailHub>(Mocker.ServiceProviderMock.Object, Mocker.LoggerFactoryMock.Object);

            var connection = new SocketConnection(Guid.NewGuid().ToString(), Mocker.ChannelMock.Object);

            await Assert.ThrowsAsync<NotSupportedException>(async () =>
            {
                await sut.OnConnectedAsync(connection);
            });

            var failHubActivatorMock = Mocker.GetHubActivator<FailHub>();
            failHubActivatorMock.Verify(m => m.Release(It.IsAny<FailHub>()), Times.Once);
        }

        [Fact]
        public async Task OnDisconnectedAsync_ShouldInvokeHubCreateAndRelease()
        {
            var sut = new HubWebSocketHandler<DefaultHub>(Mocker.ServiceProviderMock.Object, Mocker.LoggerFactoryMock.Object);

            var connection = new SocketConnection(Guid.NewGuid().ToString(), Mocker.ChannelMock.Object);

            await sut.OnDisconnectedAsync(connection, null);

            var defaultHubActivatorMock = Mocker.GetHubActivator<DefaultHub>();
            defaultHubActivatorMock.Verify(m => m.Create(), Times.Once);
            defaultHubActivatorMock.Verify(m => m.Release(It.IsAny<DefaultHub>()), Times.Once);
        }

        [Fact]
        public async Task OnDisconnectedAsync_ShouldInvokeBackplane_OnClientDisconnectedAsync()
        {
            var sut = new HubWebSocketHandler<DefaultHub>(Mocker.ServiceProviderMock.Object, Mocker.LoggerFactoryMock.Object);

            var connectionId = Guid.NewGuid().ToString();
            var connection = new SocketConnection(connectionId, Mocker.ChannelMock.Object);

            await sut.OnDisconnectedAsync(connection, null);

            Mocker.BackplaneMock.Verify(m => m.OnClientDisconnectedAsync(It.Is<string>(v => v == connectionId)), Times.Once);
        }

        [Fact]
        public async Task OnDisconnectedAsync_ReleaseShouldBeInvoked_IfHubOnDisconnectedAsyncThrows()
        {
            var sut = new HubWebSocketHandler<FailHub>(Mocker.ServiceProviderMock.Object, Mocker.LoggerFactoryMock.Object);

            var connection = new SocketConnection(Guid.NewGuid().ToString(), Mocker.ChannelMock.Object);

            await Assert.ThrowsAsync<NotSupportedException>(async () =>
            {
                await sut.OnDisconnectedAsync(connection, null);
            });

            var failHubActivatorMock = Mocker.GetHubActivator<FailHub>();
            failHubActivatorMock.Verify(m => m.Release(It.IsAny<FailHub>()), Times.Once);
        }

        [Fact]
        public async Task OnDisconnectedAsync_Throws_ShouldInvokeBackplane_OnClientDisconnectedAsync()
        {
            var sut = new HubWebSocketHandler<FailHub>(Mocker.ServiceProviderMock.Object, Mocker.LoggerFactoryMock.Object);

            var connectionId = Guid.NewGuid().ToString();
            var connection = new SocketConnection(connectionId, Mocker.ChannelMock.Object);

            await Assert.ThrowsAsync<NotSupportedException>(async () =>
            {
                await sut.OnDisconnectedAsync(connection, null);
            });

            Mocker.BackplaneMock.Verify(m => m.OnClientDisconnectedAsync(It.Is<string>(v => v == connectionId)), Times.Once);
        }
    }
}
