using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MorseL.Client.WebSockets.Tests
{
    [Trait("Target", "WebSocketClient")]
    public class WebSocketClientTests
    {
        private const string HostUri = "ws://localhost:5000";
        private const string InvalidHostUri = "ws://asdfasdfasdf:5000";

        // TODO : The internal web socket also times out - but with a generic SocketException...
        [Fact]
        public async Task ConnectTimesOutAfterXSecondsAndThrowsException()
        {
            using (var tcpListener = new SimpleTcpListener(new IPEndPoint(IPAddress.Any, 5000)))
            {
                tcpListener.Start();

                var client = new WebSocketClient(HostUri);

                await Assert.ThrowsAnyAsync<Exception>(() => client.ConnectAsync());
            }
        }

        [Fact]
        public async Task DisconnectingUnopenClientThrowsException()
        {
            var client = new WebSocketClient(HostUri);
            await Assert.ThrowsAsync<WebSocketClientException>(() => client.CloseAsync());
        }

        [Fact]
        public async Task DisconnectingConnectingClientThrowsWebSocketClientException()
        {
            var client = new WebSocketClient(HostUri);
            var connectTask = client.ConnectAsync();
            await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromSeconds(1)));

            await Assert.ThrowsAsync<WebSocketClientException>(() => client.CloseAsync());
        }

        [Fact]
        public async Task ConnectCalledOnConnectComplete()
        {
            using (new SimpleWebSocketServer(IPAddress.Any, 5000).Start())
            {
                bool connectedCalled = false;
                var client = new WebSocketClient(HostUri);
                client.Connected += () => connectedCalled = true;
                await client.ConnectAsync();
                Assert.True(connectedCalled);
            }
        }

        [Fact]
        public Task CancellationTokenCancelsConnectRequest()
        {
            using (new SimpleWebSocketServer(IPAddress.Any, 5000).Start())
            {
                var client = new WebSocketClient(HostUri);

                var cts = new CancellationTokenSource();
                var connectTask = client.ConnectAsync(cts.Token);

                cts.Cancel();
                Assert.True(cts.IsCancellationRequested);
                Assert.True(connectTask.IsCanceled);
            }

            return Task.CompletedTask;
        }

        [Fact]
        public async Task ConnectingToInvalidHostThrowsException()
        {
            var client = new WebSocketClient(InvalidHostUri);
            await Assert.ThrowsAsync<SocketException>(() => client.ConnectAsync());
        }
    }
}
