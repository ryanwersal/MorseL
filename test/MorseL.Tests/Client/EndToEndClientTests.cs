using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MorseL.Client;
using MorseL.Common;
using MorseL.Shared.Tests;
using MorseL.Shared.Tests.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace MorseL.Tests.Client
{
    public class Context
    {
        public readonly PortPool PortPool = new PortPool(6000, 6050);
    }

    [Trait("Category", "Client")]
    public class EndToEndClientTests : IClassFixture<Context>
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private ILogger _logger;
        private Context _context;

        public EndToEndClientTests(Context context, ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            _logger = new TestOutputHelperLogger(_testOutputHelper);
            _context = context;
        }

        [Fact]
        public async void ShouldThrowOnMoreThanOneCallToStartAsync()
        {
            using (var server = new SimpleMorseLServer<EndToEndTests.TestHub>(logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Connection(server.Uri, null, o => o.ThrowOnMissingHubMethodInvoked = true, logger: _logger);
                await client.StartAsync();
                await AssertEx.ThrowsAsync<MorseLException>(
                    () => client.StartAsync(),
                    e => e.Message.Equals("Cannot call StartAsync more than once.", StringComparison.Ordinal));
                await client.DisposeAsync();
            }
        }

        [Fact]
        public async void ShouldThrowOnCallToStartAsyncAfterDisposeAsync()
        {
            using (var server = new SimpleMorseLServer<EndToEndTests.TestHub>(logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Connection(server.Uri, null, o => o.ThrowOnMissingHubMethodInvoked = true, logger: _logger);
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
            using (var server = new SimpleMorseLServer<EndToEndTests.TestHub>(logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Connection(server.Uri, null, o => o.ThrowOnMissingHubMethodInvoked = true, logger: _logger);
                await client.StartAsync();
                await client.DisposeAsync();
            }
        }

        [Fact]
        public async void ShouldThrowOnMoreThanOneCallToDisposeAsyncAfterStartAsync()
        {
            using (var server = new SimpleMorseLServer<EndToEndTests.TestHub>(logger: _logger))
            {
                await Task.Delay(1000);
                await server.Start(_context.PortPool);

                var client = new Connection(
                    server.Uri,
                    null, o => o.ThrowOnMissingHubMethodInvoked = true,
                    logger: _logger);
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
            using (var server = new SimpleMorseLServer<EndToEndTests.TestHub>(logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Connection(server.Uri, null, o => o.ThrowOnMissingHubMethodInvoked = true, logger: _logger);
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
            using (var server = new SimpleMorseLServer<EndToEndTests.TestHub>(logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Connection(server.Uri, null, o => o.ThrowOnMissingHubMethodInvoked = true, logger: _logger);
                await client.StartAsync();
                await client.DisposeAsync();
                await AssertEx.ThrowsAsync<MorseLException>(
                    () => client.DisposeAsync(),
                    e => e.Message.Equals("This connection has already been disposed.", StringComparison.Ordinal));
            }
        }
    }
}
