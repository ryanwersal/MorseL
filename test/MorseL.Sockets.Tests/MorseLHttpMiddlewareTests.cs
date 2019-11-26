using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MorseL.Shared.Tests;
using MorseLIMiddleware = MorseL.Sockets.Middleware.IMiddleware;
using Xunit;
using System.Net.WebSockets;
using System.Threading;
using System.Text;

namespace MorseL.Sockets.Test
{
    [Trait("Category", "Middleware")]
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

        [Fact]
        public async Task Additional_Middleware_Should_Be_Given_The_Mutated_Context()
        {
            var sut = CreateMorseLHttpMiddleware();

            var middleware = new List<MorseL.Sockets.Middleware.IMiddleware>
            {
                new TestMiddleware("First"),
                new TestMiddleware("Second"),
                new TestMiddleware("Third")
            };

            string originalContents = "test stream data";
            string contents = null;

            var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(originalContents));
            var mockChannel = new Mock<IChannel>();
            var connection = new Connection("connectionId", mockChannel.Object);
            var connectionContext = new MorseL.Sockets.Middleware.ConnectionContext(connection, inputStream);
            
            var delegator = sut.BuildMiddlewareDelegate(middleware.GetEnumerator(), async stream => {
                using (var memStream = new MemoryStream())
                {
                    await stream.CopyToAsync(memStream);
                    contents = Encoding.UTF8.GetString(memStream.ToArray());
                }
            });

            await delegator.Invoke(connectionContext);

            string expectedResults = $"RECEIVEDThird:RECEIVEDSecond:RECEIVEDFirst:{originalContents}";

            Assert.Equal(expectedResults, contents);
        }

        private class TestMiddleware : MorseL.Sockets.Middleware.IMiddleware
        {
            private string _prefix;
            public TestMiddleware(string prefix)
            {
                _prefix = prefix;
            }

            public async Task SendAsync(MorseL.Sockets.Middleware.ConnectionContext context, MorseL.Sockets.Middleware.MiddlewareDelegate next)
            {
                string contents = null;
                using (var memStream = new MemoryStream())
                {
                    await context.Stream.CopyToAsync(memStream);
                    contents = Encoding.UTF8.GetString(memStream.ToArray());
                }

                contents = $"SENT{_prefix}:{contents}";
                await next(new MorseL.Sockets.Middleware.ConnectionContext(context.Connection, new MemoryStream(Encoding.UTF8.GetBytes(contents))));
            }

            public async Task ReceiveAsync(MorseL.Sockets.Middleware.ConnectionContext context, MorseL.Sockets.Middleware.MiddlewareDelegate next)
            {
                string contents = null;
                using (var memStream = new MemoryStream())
                {
                    await context.Stream.CopyToAsync(memStream);
                    contents = Encoding.UTF8.GetString(memStream.ToArray());
                }

                contents = $"RECEIVED{_prefix}:{contents}";
                await next(new MorseL.Sockets.Middleware.ConnectionContext(context.Connection, new MemoryStream(Encoding.UTF8.GetBytes(contents))));
            }
        }
    }
}
