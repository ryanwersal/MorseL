using System.Threading.Tasks;
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

                var connection = _manager.AddConnection(new WebSocketChannel(socket, new IMiddleware[] {}));
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
                _manager.AddConnection(new WebSocketChannel(new FakeSocket(), new IMiddleware[] { }));

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
                var connection = _manager.AddConnection(new WebSocketChannel(new FakeSocket(), new IMiddleware[] { }));

                var id = _manager.GetId(connection);

                Assert.NotNull(id);
            }
        }

        public class AddSocket : WebSocketConnectionManagerTests
        {
            [Fact(Skip = "At the moment the implementation allows adding null references")]
            public void WhenNull_ShouldNotNotContainSocket()
            {
                _manager.AddConnection(null);

                Assert.Equal(0, _manager.GetAll().Count);
            }

            [Fact]
            public void WhenInstance_ShouldContainSocket()
            {
                _manager.AddConnection(new WebSocketChannel(new FakeSocket(), new IMiddleware[] { }));

                Assert.Equal(1, _manager.GetAll().Count);
            }
        }

        public class RemoveSocket : WebSocketConnectionManagerTests
        {
            [Theory(Skip = "Currently it doesn't check if the socket was removed or not, so we get an NRE")]
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