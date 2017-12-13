using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace MorseL.Client.WebSockets.Tests
{
    [Trait("Target", "WebSocketClient")]
    public class WebSocketClientTests
    {
        // TODO : The internal web socket also times out - but with a generic SocketException...
        [Fact(DisplayName = nameof(ConnectTimesOutAfterXSecondsAndThrowsException))]
        public async Task ConnectTimesOutAfterXSecondsAndThrowsException()
        {
            using (var tcpListener = new SimpleTcpListener(new IPEndPoint(IPAddress.Any, 5000)))
            {
                tcpListener.Start();

                var client = new WebSocketClient("ws://localhost:5000");

                await Assert.ThrowsAnyAsync<Exception>(() => client.ConnectAsync());
            }
        }

        [Fact(DisplayName = nameof(DisconnectingUnopenClientThrowsException))]
        public async Task DisconnectingUnopenClientThrowsException()
        {
            var client = new WebSocketClient("ws://localhost:5000");
            try
            {
                await client.CloseAsync();
            }
            catch (Exception e)
            {
                Assert.Equal(e.Message, "The socket isn't open.");
            }
        }

        [Fact(DisplayName = nameof(ConnectCalledOnConnectComplete))]
        public async Task ConnectCalledOnConnectComplete()
        {
            using (new SimpleWebSocketServer(IPAddress.Any, 5000).Start())
            {
                bool connectedCalled = false;
                var client = new WebSocketClient("ws://localhost:5000");
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
                var client = new WebSocketClient("ws://localhost:5000");

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
            var client = new WebSocketClient("ws://asdfasdfasdf:5000");
            await Assert.ThrowsAsync<SocketException>(() => client.ConnectAsync());
        }
    }
}
