using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using MorseL.Common;
using MorseL.Shared.Tests.Extensions;
using Xunit;

namespace MorseL.Client.Tests
{
    [Trait("Category", "Client")]
    public class ClientTests
    {
        [Fact]
        public async Task ShouldThrowOnConnectToInvalidHost()
        {
            var connection = new Connection("ws://asdfasdf:5000");
            await Assert.ThrowsAsync<WebSocketException>(() => connection.StartAsync());
        }

        [Fact]
        public async Task ShouldThrowOnInvokeAsyncWhenNotConnected()
        {
            var connection = new Connection("ws://asdfasdf:5000");
            await AssertEx.ThrowsAsync<MorseLException>(
                () => connection.Invoke("SomeMethodName"),
                exception => exception.Message.Equals("Cannot call Invoke when not connected.", StringComparison.Ordinal)
            );
        }
    }
}
