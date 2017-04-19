using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace AsyncWebSocketClient.Tests
{
    public class WebSocketClientTests
    {
        [Fact]
        [Trait("project", "AsyncWebSocketClient")]
        public void ConnectTimesOutAfterFiveSeconds()
        {
            WebSocketClient client = new WebSocketClient("no.where.com");
            var time = DateTime.Now;
            var task = client.ConnectAsync();
            task.Wait();
            Assert.False(task.IsCompleted);
            Assert.True(task.IsFaulted);
        }
    }
}
