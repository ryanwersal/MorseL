using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MorseL.Shared.Tests;
using MorseL.Sockets;
using MorseL.Sockets.Middleware;
using Xunit;

namespace MorseL.Tests
{
    [Trait("Target", "WebSocketConnectionManager")]
    public class WebSocketConnectionManagerTests
    {
        private readonly WebSocketConnectionManager _manager;

        public WebSocketConnectionManagerTests()
        {
            _manager = new WebSocketConnectionManager();
        }

        public class GetSocketById : WebSocketConnectionManagerTests
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("foo")]
            public void WhenNonExistentId_ShouldReturnNull(string id)
            {
                var socket = _manager.GetConnectionById(id);

                Assert.Null(socket);
            }

            [Fact]
            public void WhenExistingId_ShouldReturnSocket()
            {
                var socket = new FakeSocket();

                ILoggerFactory loggerFactory = new LoggerFactory();
                var connection = _manager.AddConnection(new WebSocketChannel(socket, new IMiddleware[] {}, loggerFactory));
                var id = _manager.GetId(connection);

                Assert.Same(socket, ((WebSocketChannel)_manager.GetConnectionById(id).Channel).Socket);
            }
        }

        public class GetAll : WebSocketConnectionManagerTests
        {
            [Fact]
            public void WhenEmpty_ShouldReturnZero()
            {
                Assert.Equal(0, _manager.GetAll().Count);
            }

            [Fact]
            public void WhenOneSocket_ShouldReturnOne()
            {
                ILoggerFactory loggerFactory = new LoggerFactory();
                _manager.AddConnection(new WebSocketChannel(new FakeSocket(), new IMiddleware[] { }, loggerFactory));

                Assert.Equal(1, _manager.GetAll().Count);
            }
        }

        public class GetId : WebSocketConnectionManagerTests
        {
            [Fact]
            public void WhenNull_ShouldReturnNull()
            {
                var id = _manager.GetId((Connection)null);

                Assert.Null(id);
            }

            [Fact]
            public void WhenUntrackedInstance_ShouldReturnNull()
            {
                var id = _manager.GetId(new Connection("", null));

                Assert.Null(id);
            }

            [Fact]
            public void WhenTrackedInstance_ShouldReturnId()
            {
                var socket = new FakeSocket();
                ILoggerFactory loggerFactory = new LoggerFactory();
                var connection = _manager.AddConnection(new WebSocketChannel(socket, new IMiddleware[] { }, loggerFactory));

                var id = _manager.GetId(connection);

                Assert.NotNull(id);
            }
        }

        public class AddSocket : WebSocketConnectionManagerTests
        {
            [Fact]
            public void AddingNullSocket_ThrowsArgumentNullException()
            {
                Assert.Throws<ArgumentNullException>(() => _manager.AddConnection(null));
            }

            [Fact]
            public void WhenInstance_ShouldContainSocket()
            {
                ILoggerFactory loggerFactory = new LoggerFactory();
                _manager.AddConnection(new WebSocketChannel(new FakeSocket(), new IMiddleware[] { }, loggerFactory));

                Assert.Equal(1, _manager.GetAll().Count);
            }
        }

        public class RemoveSocket : WebSocketConnectionManagerTests
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("foo")]
            public async Task WhenNonExistentId_ShouldNotThrowException(string id)
            {
                await _manager.RemoveConnection(id);
            }
        }
    }
}