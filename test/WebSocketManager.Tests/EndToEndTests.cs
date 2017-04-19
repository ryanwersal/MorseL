using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using AsyncWebSocketClient.Tests;
using WebSocketManager.Client;
using Xunit;

namespace WebSocketManager.Tests
{
    [Trait("Target", "EndToEndTests")]
    public class EndToEndTests
    {
        [Fact(DisplayName = nameof(ConnectedCalledWhenClientConnectionEstablished))]
        public async void ConnectedCalledWhenClientConnectionEstablished()
        {
            using (new SimpleWebSocketManagerServer<TestHub>(IPAddress.Any, 5000).Start())
            {
                var connectedCalled = false;
                var client = new Connection("ws://localhost:5000/hub");
                client.Connected += () => connectedCalled = true;
                await client.StartAsync();
                Assert.True(connectedCalled);
            }
        }

        public class TestHub : Hub
        {
        }
    }
}
