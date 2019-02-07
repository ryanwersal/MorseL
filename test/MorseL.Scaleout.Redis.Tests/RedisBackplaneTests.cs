using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MorseL.Common;
using MorseL.Common.Serialization;
using MorseL.Extensions;
using MorseL.Shared.Tests;
using MorseL.Sockets;
using StackExchange.Redis;
using Xunit;

namespace MorseL.Scaleout.Redis.Tests
{
    [Trait("Category", "Scaleout")]
    public class RedisBackplaneTests
    {
        private const string REDIS_URI = "localhost:6379";
        private int _nextId;

        [Fact]
        public async void ConnectionSubscriptionAddedOnConnect()
        {
            var serviceProvider = CreateServiceProvider(o => {
                o.AddSingleton<IBackplane, RedisBackplane>();
                o.Configure<ConfigurationOptions>(options =>
                {
                    options.EndPoints.Add(REDIS_URI);
                });
            });

            var backplane = (RedisBackplane)serviceProvider.GetRequiredService<IBackplane>();
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var connection = await CreateHubConnectionFromSocket(actualHub, webSocket);

            Assert.Contains(connection.Id, backplane.Connections.Keys);

            await actualHub.OnDisconnected(webSocket, null);
        }

        [Fact]
        public async void ClientConnectAndDisconnectCleansUpBackplaneEventHandlers()
        {
            var serviceProvider = CreateServiceProvider(o => {
                o.AddSingleton<IBackplane, RedisBackplane>();
                o.Configure<ConfigurationOptions>(options =>
                {
                    options.EndPoints.Add(REDIS_URI);
                });
            });

            var backplane = (RedisBackplane)serviceProvider.GetRequiredService<IBackplane>();
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var connection = await CreateHubConnectionFromSocket(actualHub, webSocket);

            Assert.Contains(connection.Id, backplane.Connections.Keys);

            await actualHub.OnDisconnected(webSocket, null);

            Assert.Equal(0, backplane.OnMessageCount);
        }

        [Fact]
        public async void ConnectionSubscriptionRemovedOnNormalDisconnect()
        {
            var serviceProvider = CreateServiceProvider(o => {
                o.AddSingleton<IBackplane, RedisBackplane>();
                o.Configure<ConfigurationOptions>(options =>
                {
                    options.EndPoints.Add(REDIS_URI);
                });
            });

            var backplane = (RedisBackplane)serviceProvider.GetRequiredService<IBackplane>();
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var connection = await CreateHubConnectionFromSocket(actualHub, webSocket);
            await actualHub.OnDisconnected(webSocket, null);

            Assert.DoesNotContain(connection.Id, backplane.Connections.Keys);
        }

        [Fact]
        public async void SubscriptionAddedOnSubscribe()
        {
            var serviceProvider = CreateServiceProvider(o => {
                o.AddSingleton<IBackplane, RedisBackplane>();
                o.Configure<ConfigurationOptions>(options =>
                {
                    options.EndPoints.Add(REDIS_URI);
                });
            });

            var backplane = (RedisBackplane) serviceProvider.GetRequiredService<IBackplane>();
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var connection = await CreateHubConnectionFromSocket(actualHub, webSocket);

            await backplane.Subscribe("some-group", connection.Id);

            Assert.Contains("some-group", backplane.Groups.Keys);
        }

        [Fact]
        public async void SubscriptionRemovedOnDisconnect()
        {
            var serviceProvider = CreateServiceProvider(o => {
                o.AddSingleton<IBackplane, RedisBackplane>();
                o.Configure<ConfigurationOptions>(options =>
                {
                    options.EndPoints.Add(REDIS_URI);
                });
            });

            var backplane = (RedisBackplane)serviceProvider.GetRequiredService<IBackplane>();
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var connection = await CreateHubConnectionFromSocket(actualHub, webSocket);

            await backplane.Subscribe("some-group", connection.Id);

            await actualHub.OnDisconnected(webSocket, null);

            Assert.DoesNotContain("some-group", backplane.Groups.Keys);
            Assert.DoesNotContain(connection.Id, backplane.Subscriptions.Keys);
        }

        [Fact]
        public async void SubscriptionRemovedOnUnsubscribe()
        {
            var serviceProvider = CreateServiceProvider(o => {
                o.AddSingleton<IBackplane, RedisBackplane>();
                o.Configure<ConfigurationOptions>(options =>
                {
                    options.EndPoints.Add(REDIS_URI);
                });
            });

            var backplane = (RedisBackplane)serviceProvider.GetRequiredService<IBackplane>();
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var connection = await CreateHubConnectionFromSocket(actualHub, webSocket);

            // Subscribe to n groups
            await backplane.Subscribe("some-group", connection.Id);
            await backplane.Subscribe("some-other-group", connection.Id);

            // Make sure our group list contains them
            Assert.Contains("some-group", backplane.Groups.Keys);
            Assert.Contains("some-other-group", backplane.Groups.Keys);

            // Make sure out subscription list contains them
            Assert.Contains(connection.Id, backplane.Subscriptions.Keys);
            Assert.Contains("some-group", backplane.Subscriptions[connection.Id].Keys);
            Assert.Contains("some-other-group", backplane.Subscriptions[connection.Id].Keys);

            // Unsubscribe from one group
            await backplane.Unsubscribe("some-group", connection.Id);

            // Validate that group has been removed
            Assert.DoesNotContain("some-group", backplane.Groups.Keys);
            Assert.DoesNotContain("some-group", backplane.Subscriptions[connection.Id].Keys);

            // Make sure the other group is still subscribed
            Assert.Contains("some-other-group", backplane.Groups.Keys);
            Assert.Contains(connection.Id, backplane.Subscriptions.Keys);
            Assert.Contains("some-other-group", backplane.Subscriptions[connection.Id].Keys);

            // Unsubscribe from the final group
            await backplane.Unsubscribe("some-other-group", connection.Id);

            // Validate that both groups have been unsubscribed and individual collections are gone
            Assert.DoesNotContain("some-group", backplane.Groups.Keys);
            Assert.DoesNotContain("some-other-group", backplane.Groups.Keys);
            Assert.DoesNotContain(connection.Id, backplane.Subscriptions.Keys);
        }

        [Theory]
        [InlineData("Some message")]
        public async void MessageSentToSubscribedGroup(string messageText)
        {
            var serviceProvider = CreateServiceProvider(o => {
                o.AddSingleton<IBackplane, RedisBackplane>();
                o.Configure<ConfigurationOptions>(options =>
                {
                    options.EndPoints.Add(REDIS_URI);
                });
            });

            var backplane = (RedisBackplane)serviceProvider.GetRequiredService<IBackplane>();
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var connection = await CreateHubConnectionFromSocket(actualHub, webSocket);

            await backplane.Subscribe("some-group", connection.Id);
            await backplane.SendMessageGroupAsync("some-group", new Message()
            {
                MessageType = MessageType.Text,
                Data = messageText
            });

            await Task.Delay(1000);

            var message = await ReadMessageFromSocketAsync(webSocket);

            Assert.Equal(MessageType.Text, message.MessageType);
            Assert.Equal(messageText, message.Data);
        }

        private IServiceProvider CreateServiceProvider(Action<ServiceCollection> addServices = null)
        {
            var services = new ServiceCollection();
            services.AddOptions()
                .AddLogging()
                .AddMorseL();

            addServices?.Invoke(services);

            return services.BuildServiceProvider();
        }

        private async Task<Connection> CreateHubConnectionFromSocket(HubWebSocketHandler<TestHub> actualHub, LinkedFakeSocket webSocket)
        {
            var connection = await actualHub.OnConnected(webSocket, new DefaultHttpContext());

            // Receive the connection message
            var connectMessage = await ReadMessageFromSocketAsync(webSocket);

            Assert.NotNull(connectMessage);
            Assert.NotNull(connectMessage.Data);
            Assert.NotNull(Guid.Parse(connectMessage.Data));
            return connection;
        }

        private async Task<Message> ReadMessageFromSocketAsync(WebSocket socket)
        {
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[1024 * 4]);
            string serializedMessage;

            using (var ms = new MemoryStream())
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
                    ms.Write(buffer.Array, buffer.Offset, result.Count);
                }
                while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);

                using (var reader = new StreamReader(ms, Encoding.UTF8))
                {
                    serializedMessage = await reader.ReadToEndAsync().ConfigureAwait(false);
                }
            }

            return MessageSerializer.Deserialize<Message>(serializedMessage);
        }

        private async Task<InvocationResultDescriptor> ReadInvocationResultFromSocket<TReturnType>(WebSocket socket)
        {
            var message = await ReadMessageFromSocketAsync(socket);
            var pendingCalls = new Dictionary<string, InvocationRequest>();
            pendingCalls.Add(_nextId.ToString(), new InvocationRequest(new CancellationToken(), typeof(TReturnType)));
            return MessageSerializer.DeserializeInvocationResultDescriptor(message.Data, pendingCalls);
        }
    }

    public class TestHub : Hub
    {
        public static bool SomeMethodCalled = false;

        public void SomeMethod()
        {
            SomeMethodCalled = true;
        }
    }
}