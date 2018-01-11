using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using MorseL.Client;
using MorseL.Common;
using MorseL.Shared.Tests;
using MorseL.Shared.Tests.Extensions;
using Xunit;

namespace MorseL.Tests.Client
{
    public class EndToEndClientTests
    {
        [Fact]
        public async void ShouldThrowOnMoreThanOneCallToStartAsync()
        {
            using (new SimpleMorseLServer<EndToEndTests.TestHub>(IPAddress.Any, 54321).Start())
            {
                var client = new Connection("ws://localhost:54321/hub", null, o => o.ThrowOnMissingHubMethodInvoked = true);
                await client.StartAsync();
                await AssertEx.ThrowsAsync<MorseLException>(
                    () => client.StartAsync(),
                    e => e.Message.Equals("Cannot call StartAsync more than once.", StringComparison.Ordinal));
            }
        }

        [Fact]
        public async void ShouldThrowOnCallToStartAsyncAfterDisposeAsync()
        {
            using (new SimpleMorseLServer<EndToEndTests.TestHub>(IPAddress.Any, 54321).Start())
            {
                var client = new Connection("ws://localhost:54321/hub", null, o => o.ThrowOnMissingHubMethodInvoked = true);
                await client.StartAsync();
                await client.DisposeAsync();
                await AssertEx.ThrowsAsync<MorseLException>(
                    () => client.StartAsync(),
                    e => e.Message.Equals("Cannot call StartAsync more than once."));
            }
        }

        [Fact]
        public async void ShouldNotThrowOnCallToDisposeAsyncBeforeStartAsync()
        {
            using (new SimpleMorseLServer<EndToEndTests.TestHub>(IPAddress.Any, 54321).Start())
            {
                var client = new Connection("ws://localhost:54321/hub", null, o => o.ThrowOnMissingHubMethodInvoked = true);
                await client.StartAsync();
                await client.DisposeAsync();
            }
        }

        [Fact]
        public async void ShouldThrowOnMoreThanOneCallToDisposeAsyncAfterStartAsync()
        {
            using (new SimpleMorseLServer<EndToEndTests.TestHub>(IPAddress.Any, 54321).Start())
            {
                var client = new Connection("ws://localhost:54321/hub", null, o => o.ThrowOnMissingHubMethodInvoked = true);
                await client.StartAsync();
                await client.DisposeAsync();
                await AssertEx.ThrowsAsync<MorseLException>(
                    () => client.DisposeAsync(),
                    e => e.Message.Equals("This connection has already been disposed.", StringComparison.Ordinal));
            }
        }

        [Fact]
        public async Task ShouldThrowOnInvokeAsyncWhenNotConnected()
        {
            using (new SimpleMorseLServer<EndToEndTests.TestHub>(IPAddress.Any, 54321).Start())
            {
                var client = new Connection("ws://localhost:54321/hub", null, o => o.ThrowOnMissingHubMethodInvoked = true);
                await client.StartAsync();
                await client.DisposeAsync();
                await AssertEx.ThrowsAsync<MorseLException>(
                    () => client.DisposeAsync(),
                    e => e.Message.Equals("This connection has already been disposed.", StringComparison.Ordinal));
            }
        }

        [Fact]
        public async Task ShouldThrowOnInvokeAsyncWhenDisposed()
        {
            using (new SimpleMorseLServer<EndToEndTests.TestHub>(IPAddress.Any, 54321).Start())
            {
                var client = new Connection("ws://localhost:54321/hub", null, o => o.ThrowOnMissingHubMethodInvoked = true);
                await client.StartAsync();
                await client.DisposeAsync();
                await AssertEx.ThrowsAsync<MorseLException>(
                    () => client.DisposeAsync(),
                    e => e.Message.Equals("This connection has already been disposed.", StringComparison.Ordinal));
            }
        }
    }
}
