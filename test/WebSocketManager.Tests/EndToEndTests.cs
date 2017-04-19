using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
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

        [Fact(DisplayName = nameof(ReconnectingDoesNotKillServer))]
        public async void ReconnectingDoesNotKillServer()
        {
            using (new SimpleWebSocketManagerServer<TestHub>(IPAddress.Any, 5000).Start())
            {
                var connectedCalled = false;
                for (int i = 0; i < 10; i++)
                {
                    var client = new Connection("ws://localhost:5000/hub");
                    client.Connected += () => connectedCalled = true;
                    await client.StartAsync();
                    var task = client.Invoke<object>("FooBar");
                    await Task.Delay(100);
                }
                Assert.True(connectedCalled);
            }
        }

        public class TestHub : Hub
        {
            public void FooBar() { }
        }
    }
}
