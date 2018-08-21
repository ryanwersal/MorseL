using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
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
using Xunit;

namespace MorseL.Scaleout.Tests
{
    public class ScaleoutTests
    {
        private int _nextId;

        [Fact]
        public async void ClientConnectCallsBackplaneOnClientConnected()
        {
            var backplane = new TestBackplane();
            var serviceProvider = CreateServiceProvider(o => {
                o.AddSingleton<IBackplane>(backplane);
            });
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var exception = await Assert.ThrowsAnyAsync<NotImplementedException>(
                () => CreateHubConnectionFromSocket(actualHub, webSocket));
            Assert.Equal(nameof(TestBackplane.OnClientConnectedAsync), exception.Message);
        }

        [Fact]
        public async void ClientConnectAndDisconnectCleansUpBackplaneEventHandlers()
        {
            var backplane = new DefaultBackplane();
            var serviceProvider = CreateServiceProvider(o => {
                o.AddSingleton<IBackplane>(backplane);
            });
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var connection = await CreateHubConnectionFromSocket(actualHub, webSocket);
            await actualHub.OnDisconnected(webSocket, null);

            Assert.Equal(0, backplane.OnMessageCount);
        }

        [Fact]
        public async void ClientDisconnectCallsBackplaneOnClientDisconnected()
        {
            var backplane = new TestBackplane() {
                OnClientConnectedCallback = (id) => Task.CompletedTask
            };
            var serviceProvider = CreateServiceProvider(o => {
                o.AddSingleton<IBackplane>(backplane);
            });
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var connection = await CreateHubConnectionFromSocket(actualHub, webSocket);
            var exception = await Assert.ThrowsAnyAsync<NotImplementedException>(
                () => actualHub.OnDisconnected(webSocket, null));
            Assert.Equal(nameof(TestBackplane.OnClientDisconnectedAsync), exception.Message);
        }

        [Fact]
        public async void SubscriptionAddedOnSubscribe()
        {
            var backplane = new DefaultBackplane();
            var serviceProvider = CreateServiceProvider(o => {
                o.AddSingleton<IBackplane>(backplane);
            });
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var connection = await CreateHubConnectionFromSocket(actualHub, webSocket);

            await backplane.Subscribe("some-group", connection.Id);

            Assert.Contains("some-group", backplane.Groups.Keys);
        }

        [Fact]
        public async void SubscriptionRemovedOnDisconnect()
        {
            var backplane = new DefaultBackplane();
            var serviceProvider = CreateServiceProvider(o => {
                o.AddSingleton<IBackplane>(backplane);
            });
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
            var backplane = new DefaultBackplane();
            var serviceProvider = CreateServiceProvider(o => {
                o.AddSingleton<IBackplane>(backplane);
            });
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var connection = await CreateHubConnectionFromSocket(actualHub, webSocket);

            await backplane.Subscribe("some-group", connection.Id);

            Assert.Contains("some-group", backplane.Groups.Keys);

            await backplane.Unsubscribe("some-group", connection.Id);

            Assert.DoesNotContain("some-group", backplane.Groups.Keys);
            Assert.DoesNotContain(connection.Id, backplane.Subscriptions.Keys);
        }

        [Theory]
        [InlineData("Some message")]
        public async void MessageSentToSubscribedGroup(string messageText)
        {
            var backplane = new DefaultBackplane();
            var serviceProvider = CreateServiceProvider(o => {
                o.AddSingleton<IBackplane>(backplane);
            });
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var connection = await CreateHubConnectionFromSocket(actualHub, webSocket);

            await backplane.Subscribe("some-group", connection.Id);
            await backplane.SendMessageGroupAsync("some-group", new Message()
            {
                MessageType = MessageType.Text,
                Data = messageText
            });

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

            return Json.Deserialize<Message>(serializedMessage);
        }

        private async Task<InvocationResultDescriptor> ReadInvocationResultFromSocket<TReturnType>(WebSocket socket)
        {
            var message = await ReadMessageFromSocketAsync(socket);
            var pendingCalls = new Dictionary<string, InvocationRequest>();
            pendingCalls.Add(_nextId.ToString(), new InvocationRequest(new CancellationToken(), typeof(TReturnType)));
            return Json.DeserializeInvocationResultDescriptor(message.Data, pendingCalls);
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