using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MorseL.Shared.Tests;
using StackExchange.Redis;
using Xunit;

namespace MorseL.Scaleout.Redis.Tests
{
    public class EndToEndTests
    {
        [Fact]
        public async void ConnectionSubscriptionAddedOnConnect()
        {
            RedisBackplane backplane = null;
            using (new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000, (collection, builder) =>
            {
                collection.AddSingleton<IBackplane, RedisBackplane>();
                collection.Configure<ConfigurationOptions>(options =>
                {
                    options.EndPoints.Add("localhost:6379");
                });
            }, (builder, provider) =>
            {
                backplane = (RedisBackplane) provider.GetRequiredService<IBackplane>();
            }).Start())
            {
                var client = new Client.Connection("ws://localhost:5000/hub");
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
            using (new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000, (collection, builder) =>
            {
                collection.AddSingleton<IBackplane, RedisBackplane>();
                collection.Configure<ConfigurationOptions>(options =>
                {
                    options.EndPoints.Add("localhost:6379");
                });
            }, (builder, provider) =>
            {
                backplane = (RedisBackplane)provider.GetRequiredService<IBackplane>();
            }).Start())
            {
                var client = new Client.Connection("ws://localhost:5000/hub");
                await client.StartAsync();
                await client.DisposeAsync();

                Assert.DoesNotContain(client.ConnectionId, backplane.Connections.Keys);
            }
        }

        [Fact]
        public async void ConnectionSubscriptionRemovedOnAbnormalDisconnect()
        {
            RedisBackplane backplane = null;
            using (new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000, (collection, builder) =>
            {
                collection.AddSingleton<IBackplane, RedisBackplane>();
                collection.Configure<ConfigurationOptions>(options =>
                {
                    options.EndPoints.Add("localhost:6379");
                });
            }, (builder, provider) =>
            {
                backplane = (RedisBackplane)provider.GetRequiredService<IBackplane>();
            }).Start())
            {
                var client = new Client.Connection("ws://localhost:5000/hub");
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
            using (new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000, (collection, builder) =>
            {
                collection.AddSingleton<IBackplane, RedisBackplane>();
                collection.Configure<ConfigurationOptions>(options =>
                {
                    options.EndPoints.Add("localhost:6379");
                });
            }, (builder, provider) =>
            {
                backplane = (RedisBackplane)provider.GetRequiredService<IBackplane>();
            }).Start())
            {
                var client = new Client.Connection("ws://localhost:5000/hub");
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
