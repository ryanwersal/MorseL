using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MorseL.Shared.Tests;
using MorseLIMiddleware = MorseL.Sockets.Middleware.IMiddleware;
using Xunit;
using System.Net.WebSockets;
using System.Threading;

namespace MorseL.Sockets.Test
{
    public class MorseLHttpMiddlewareTests
    {
        private readonly ServicesMocker Mocker = new ServicesMocker();

        public interface IMockRequestDelegate
        {
            Task Next(HttpContext context);
        }
        private readonly Mock<IMockRequestDelegate> RequestDelegateMock = new Mock<IMockRequestDelegate>();

        public class MiddlewareTestHub : Hub<IClientInvoker> 
        {
            public Exception DisconnectedException { get; private set; }

            public override Task OnDisconnectedAsync(Exception exception)
            {
                DisconnectedException = exception;
                return base.OnDisconnectedAsync(exception);
            }
        }
        private readonly MiddlewareTestHub Hub = new MiddlewareTestHub();

        public MorseLHttpMiddlewareTests()
        {
            Mocker.RegisterService<ILogger<MorseLHttpMiddleware>>();
            Mocker.RegisterService<IEnumerable<MorseLIMiddleware>>(DefaultValue.Mock);

            Mocker.RegisterHub<MiddlewareTestHub>(Hub);

            RequestDelegateMock.Setup(m => m.Next(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);
        }

        private MorseLHttpMiddleware CreateMorseLHttpMiddleware()
        {
            return new MorseLHttpMiddleware(
                Mocker.ServiceProviderMock.Object.GetRequiredService<ILogger<MorseLHttpMiddleware>>(),
                RequestDelegateMock.Object.Next,
                typeof(HubWebSocketHandler<MiddlewareTestHub>));
        }

        [Fact]
        public async Task NonWebSocketRequest_InvokesNextRequestDelegate()
        {
            var sut = CreateMorseLHttpMiddleware();

            Mocker.WebSocketManagerMock.Setup(m => m.IsWebSocketRequest).Returns(false);

            await sut.Invoke(Mocker.HttpContextMock.Object);

            RequestDelegateMock.Verify(m => m.Next(Mocker.HttpContextMock.Object), Times.Once);
        }

        [Fact]
        public async Task WebSocketRequest_DoesNotInvokeNextRequestDelegate()
        {
            var sut = CreateMorseLHttpMiddleware();

            await sut.Invoke(Mocker.HttpContextMock.Object);

            RequestDelegateMock.Verify(m => m.Next(It.IsAny<HttpContext>()), Times.Never);
        }

        [Fact]
        public async Task WebSocketRequest_ShouldBeAddedToConnectionManager()
        {
            var sut = CreateMorseLHttpMiddleware();

            await sut.Invoke(Mocker.HttpContextMock.Object);

            Mocker.WebSocketConnectionManagerMock.Verify(m => m.AddConnection(It.IsAny<IChannel>()), Times.Once);
        }

        [Fact]
        public async Task WebSocketRequest_ShouldBeRemovedFromConnectionManager_IfSocketIsNotOpen()
        {
            Mocker.WebSocketMock.Setup(m => m.State).Returns(WebSocketState.None);

            var sut = CreateMorseLHttpMiddleware();

            await sut.Invoke(Mocker.HttpContextMock.Object);

            Mocker.WebSocketConnectionManagerMock.Verify(m => m.RemoveConnection(Mocker.ConnectionId), Times.Once);
        }

        [Fact]
        public async Task WebSocketRequest_ShouldBeRemovedFromConnectionManager_IfExceptionThrownInReceiveLoop()
        {
            Mocker.WebSocketMock.Setup(m => m.State).Returns(WebSocketState.Open);
            Mocker.WebSocketMock.Setup(m => m.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>()))
                .Throws<Exception>();

            var sut = CreateMorseLHttpMiddleware();

            await sut.Invoke(Mocker.HttpContextMock.Object);

            Mocker.WebSocketConnectionManagerMock.Verify(m => m.RemoveConnection(Mocker.ConnectionId), Times.Once);
        }

        [Fact]
        public async Task WebSocketRequest_ShouldBeRemovedFromConnectionManager_IfCloseMessageIsReceived()
        {
            Mocker.WebSocketMock.Setup(m => m.State).Returns(WebSocketState.Open);

            var sut = CreateMorseLHttpMiddleware();

            // Fire and forget since this is essentially just an infinite loop
            var _ = sut.Invoke(Mocker.HttpContextMock.Object);

            // We eventually need to transition the socket to closed so as to simulate
            // a proper close of the web socket.
            await Task.Delay(TimeSpan.FromMilliseconds(5));
            Mocker.WebSocketMock.Setup(m => m.State).Returns(WebSocketState.Closed);

            Mocker.WebSocketConnectionManagerMock.Verify(m => m.RemoveConnection(Mocker.ConnectionId), Times.AtLeastOnce); 
        }

        [Fact]
        public async Task WebSocketRequest_ExceptionThrownInReceiveLoop_ShouldPassExceptionToOnDisconnected()
        {
            var exception = new Exception("Expected Exception");
            Mocker.WebSocketMock.Setup(m => m.State).Throws(exception);

            var sut = CreateMorseLHttpMiddleware();

            await sut.Invoke(Mocker.HttpContextMock.Object);

            Assert.Same(exception, Hub.DisconnectedException);
        }
    }
}
