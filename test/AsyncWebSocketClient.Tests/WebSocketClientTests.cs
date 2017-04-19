using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace AsyncWebSocketClient.Tests
{
    [Trait("Target", "WebSocketClient")]
    public class WebSocketClientTests
    {
        // TODO : The internal web socket also times out - but with a generic SocketException...
        [Theory(DisplayName = nameof(ConnectTimesOutAfterXSecondsAndThrowsException))]
        [InlineData(1)]
        [InlineData(2)]
        public void ConnectTimesOutAfterXSecondsAndThrowsException(int timeoutInSeconds)
        {
            using (var tcpListener = new SimpleTcpListener(new IPEndPoint(IPAddress.Any, 5000)))
            {
                tcpListener.Start();

                var client = new WebSocketClient("ws://localhost:5000",
                    option => option.ConnectTimeout = TimeSpan.FromSeconds(timeoutInSeconds));

                var time = DateTime.Now;

                var task = client.ConnectAsync();
                Task.WaitAny(task);

                // Tolerance: 10%
                Assert.InRange(DateTime.Now - time, TimeSpan.Zero, TimeSpan.FromSeconds(timeoutInSeconds + .1 * timeoutInSeconds));
                Assert.True(task.IsCompleted);
                Assert.True(task.IsFaulted);
                Assert.Equal(task?.Exception?.InnerException?.Message, "The operation has timed out.");
            }
        }

        [Theory(DisplayName = nameof(DisconnectTimesOutAfterXSeconds), Skip = "TODO")]
        [InlineData(1)]
        [InlineData(2)]
        public void DisconnectTimesOutAfterXSeconds(int timeoutInSeconds)
        {
            // TODO
        }

        [Fact(DisplayName = nameof(DisconnectingUnopenClientThrowsException))]
        public void DisconnectingUnopenClientThrowsException()
        {
            var client = new WebSocketClient("ws://localhost:5000");
            var task = client.DisconnectAsync();
            Task.WaitAny(task);
            Assert.True(task.IsCompleted);
            Assert.True(task.IsFaulted);
            Assert.Equal(task?.Exception?.InnerException?.Message, "The socket isn't open.");
        }
    }
}
