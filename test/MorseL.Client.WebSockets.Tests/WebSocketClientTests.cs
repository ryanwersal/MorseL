using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
        public async void ConnectTimesOutAfterXSecondsAndThrowsException()
        {
            using (var tcpListener = new SimpleTcpListener(new IPEndPoint(IPAddress.Any, 5000)))
            {
                tcpListener.Start();

                var client = new WebSocketClient("ws://localhost:5000");

                try
                {
                    await client.ConnectAsync();
                }
                catch (Exception e)
                {
                    Assert.True(e.Message.StartsWith("No connection could be made because"));
                }
            }
        }

        [Fact(DisplayName = nameof(DisconnectingUnopenClientThrowsException))]
        public async void DisconnectingUnopenClientThrowsException()
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
        public async void ConnectCalledOnConnectComplete()
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
    }
}
