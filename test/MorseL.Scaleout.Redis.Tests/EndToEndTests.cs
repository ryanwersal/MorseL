using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MorseL.Shared.Tests;
using StackExchange.Redis;
using Xunit;
using Xunit.Abstractions;

namespace MorseL.Scaleout.Redis.Tests
{
    public class Context
    {
        public readonly PortPool PortPool = new PortPool(5100, 5150);
    }

    [Trait("Category", "Scaleout")]
    public class EndToEndTests : IClassFixture<Context>
    {
        private const string REDIS_URI = "localhost:6379";

        private readonly ITestOutputHelper _testOutputHelper;
        private ILogger _logger;
        private Context _context;

        public EndToEndTests(Context context, ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            _logger = new TestOutputHelperLogger(_testOutputHelper);
            _context = context;
        }

        [Fact]
        public async void ConnectionSubscriptionAddedOnConnect()
        {
            RedisBackplane backplane = null;
            using (var server = new SimpleMorseLServer<TestHub>((collection, builder) =>
            {
                collection.AddSingleton<IBackplane, RedisBackplane>();
                collection.Configure<ConfigurationOptions>(options =>
                {
                    options.EndPoints.Add(REDIS_URI);
                });
            }, (builder, provider) =>
            {
                backplane = (RedisBackplane) provider.GetRequiredService<IBackplane>();
            }, logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Client.Connection(server.Uri, logger: _logger);
                await client.StartAsync();
                await Task.Delay(250);

                Assert.Contains(client.ConnectionId, backplane.Connections.Keys);

                await client.DisposeAsync();
            }
        }

        [Fact]
        public async void ConnectionSubscriptionRemovedOnNormalDisconnect()
        {
            RedisBackplane backplane = null;
            using (var server = new SimpleMorseLServer<TestHub>((collection, builder) =>
            {
                collection.AddSingleton<IBackplane, RedisBackplane>();
                collection.Configure<ConfigurationOptions>(options =>
                {
                    options.EndPoints.Add(REDIS_URI);
                });
            }, (builder, provider) =>
            {
                backplane = (RedisBackplane)provider.GetRequiredService<IBackplane>();
            }, logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Client.Connection(server.Uri, logger: _logger);
                await client.StartAsync();
                await client.DisposeAsync();

                Assert.DoesNotContain(client.ConnectionId, backplane.Connections.Keys);
            }
        }

        [Fact]
        public async void ConnectionSubscriptionRemovedOnAbnormalDisconnect()
        {
            RedisBackplane backplane = null;
            using (var server = new SimpleMorseLServer<TestHub>((collection, builder) =>
            {
                collection.AddSingleton<IBackplane, RedisBackplane>();
                collection.Configure<ConfigurationOptions>(options =>
                {
                    options.EndPoints.Add(REDIS_URI);
                });
            }, (builder, provider) =>
            {
                backplane = (RedisBackplane)provider.GetRequiredService<IBackplane>();
            }, logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Client.Connection(server.Uri, logger: _logger);
                await client.StartAsync();
                client.KillConnection();

                await Task.Delay(2000);

                Assert.DoesNotContain(client.ConnectionId, backplane.Connections.Keys);
            }
        }

        [Fact]
        public async Task SendingDisconnectClientAsync_DisconnectsClient()
        {
            RedisBackplane backplane = null;
            using (var server = new SimpleMorseLServer<TestHub>((collection, builder) =>
            {
                collection.AddSingleton<IBackplane, RedisBackplane>();
                collection.Configure<ConfigurationOptions>(options =>
                {
                    options.EndPoints.Add(REDIS_URI);
                });
            }, (builder, provider) =>
            {
                backplane = (RedisBackplane)provider.GetRequiredService<IBackplane>();
            }, logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Client.Connection(server.Uri, logger: _logger);
                await client.StartAsync();

                await Task.Delay(2000);

                Assert.True(client.IsConnected);
                await backplane.DisconnectClientAsync(client.ConnectionId);

                await Task.Delay(2000);

                Assert.False(client.IsConnected);
                Assert.DoesNotContain(client.ConnectionId, backplane.Connections.Keys);
            }
        }
    }
}
