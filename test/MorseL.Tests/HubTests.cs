using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MorseL.Client;
using MorseL.Common;
using MorseL.Common.Serialization;
using MorseL.Sockets;
using Xunit;

namespace MorseL.Tests
{
    [Trait("Target", "Hubs")]
    public class HubTests
    {
        private int _nextId;

        [Fact]
        public async void CanCallVoidMethodOnHub()
        {
            var serviceProvider = CreateServiceProvider();
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new FakeWebSocket();

            await actualHub.OnConnected(webSocket, new DefaultHttpContext());

            await SendMessageToSocketAsync(actualHub, webSocket, nameof(TestHub.VoidMethod), null);

            var result = await ReadMessageFromSocketAsync<object>(webSocket);

            Assert.NotNull(result);
            Assert.Null(result.Result);
        }

        [Fact]
        public async void CanCallReturnVoidAsyncMethodOnHub()
        {
            var serviceProvider = CreateServiceProvider();
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new FakeWebSocket();

            await actualHub.OnConnected(webSocket, new DefaultHttpContext());

            await SendMessageToSocketAsync(actualHub, webSocket, nameof(TestHub.VoidMethodAsync), null);

            var result = await ReadMessageFromSocketAsync<object>(webSocket);

            Assert.NotNull(result);
            Assert.Null(result.Result);
        }

        [Fact]
        public async void CanCallReturnIntMethodOnHub()
        {
            var serviceProvider = CreateServiceProvider();
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new FakeWebSocket();

            await actualHub.OnConnected(webSocket, new DefaultHttpContext());

            await SendMessageToSocketAsync(actualHub, webSocket, nameof(TestHub.IntMethod), null);

            var result = await ReadMessageFromSocketAsync<int>(webSocket);

            Assert.NotNull(result);
            Assert.Equal(result.Result, TestHub.IntResult);
        }

        [Fact]
        public async void CanCallReturnIntAsyncMethodOnHub()
        {
            var serviceProvider = CreateServiceProvider();
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new FakeWebSocket();

            await actualHub.OnConnected(webSocket, new DefaultHttpContext());

            await SendMessageToSocketAsync(actualHub, webSocket, nameof(TestHub.IntMethodAsync), null);

            var result = await ReadMessageFromSocketAsync<int>(webSocket);

            Assert.NotNull(result);
            Assert.Equal(result.Result, TestHub.IntResult);
        }

        [Fact]
        public async void CanCallReturnStringMethodOnHub()
        {
            var serviceProvider = CreateServiceProvider();
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new FakeWebSocket();

            await actualHub.OnConnected(webSocket, new DefaultHttpContext());

            await SendMessageToSocketAsync(actualHub, webSocket, nameof(TestHub.StringMethod), null);

            var result = await ReadMessageFromSocketAsync<string>(webSocket);

            Assert.NotNull(result);
            Assert.Equal(result.Result, TestHub.StringResult);
        }

        [Fact]
        public async void CanCallReturnStringAsyncMethodOnHub()
        {
            var serviceProvider = CreateServiceProvider();
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new FakeWebSocket();

            await actualHub.OnConnected(webSocket, new DefaultHttpContext());

            await SendMessageToSocketAsync(actualHub, webSocket, nameof(TestHub.StringMethodAsync), null);

            var result = await ReadMessageFromSocketAsync<string>(webSocket);

            Assert.NotNull(result);
            Assert.Equal(result.Result, TestHub.StringResult);
        }

        [Fact]
        public async void CanCallReturnFloatMethodOnHub()
        {
            var serviceProvider = CreateServiceProvider();
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new FakeWebSocket();

            await actualHub.OnConnected(webSocket, new DefaultHttpContext());

            await SendMessageToSocketAsync(actualHub, webSocket, nameof(TestHub.FloatMethod), null);

            var result = await ReadMessageFromSocketAsync<float>(webSocket);

            Assert.NotNull(result);
            Assert.Equal(result.Result, TestHub.FloatResult);
        }

        [Fact]
        public async void CanCallReturnFloatAsyncMethodOnHub()
        {
            var serviceProvider = CreateServiceProvider();
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new FakeWebSocket();

            await actualHub.OnConnected(webSocket, new DefaultHttpContext());

            await SendMessageToSocketAsync(actualHub, webSocket, nameof(TestHub.FloatMethod), null);

            var result = await ReadMessageFromSocketAsync<float>(webSocket);

            Assert.NotNull(result);
            Assert.Equal(result.Result, TestHub.FloatResult);
        }

        private async Task SendMessageToSocketAsync(WebSocketHandler handler, WebSocket webSocket, string methodName, params object[] args)
        {
            var serializedMessage = Json.SerializeObject(new InvocationDescriptor()
            {
                Id = Interlocked.Increment(ref _nextId).ToString(),
                MethodName = methodName,
                Arguments = args
            });
            await handler.ReceiveAsync(webSocket, null, serializedMessage);
        }

        private async Task<InvocationResultDescriptor> ReadMessageFromSocketAsync<TReturnType>(WebSocket socket)
        {
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[1024 * 4]);
            string serializedInvocationDescriptor;
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
                    serializedInvocationDescriptor = await reader.ReadToEndAsync().ConfigureAwait(false);
                }
            }

            var pendingCalls = new Dictionary<string, InvocationRequest>();
            pendingCalls.Add(_nextId.ToString(), new InvocationRequest(new CancellationToken(), typeof(TReturnType)));
            var message = Json.Deserialize<Message>(serializedInvocationDescriptor);
            return Json.DeserializeInvocationResultDescriptor(message.Data, pendingCalls);
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

        public class FakeWebSocket : WebSocket
        {
            public override void Abort() { }

            public override void Dispose() { }

            public override WebSocketCloseStatus? CloseStatus => WebSocketCloseStatus.NormalClosure;
            public override string CloseStatusDescription => "";
            public override WebSocketState State => WebSocketState.Open;
            public override string SubProtocol => "";

            private byte[] _buffer;
            private int _position = 0;

            public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage,
                CancellationToken cancellationToken)
            {
                _buffer = new byte[buffer.Array.Length];
                Array.Copy(buffer.Array, buffer.Offset, _buffer, buffer.Offset, buffer.Count);
                return Task.CompletedTask;
            }

            public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            {
                WebSocketReceiveResult result;

                var bytesRead = Math.Min(_buffer.Length - _position, buffer.Array.Length);
                var bytesLeft = _buffer.Length - _position - bytesRead;
                if (bytesRead > 0)
                {
                    result = new WebSocketReceiveResult(bytesRead, WebSocketMessageType.Text, bytesLeft <= 0);
                }
                else
                {
                    result = new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
                }

                Array.Copy(_buffer, _position, buffer.Array, 0, bytesRead);
                _position += bytesRead;

                return Task.FromResult(result);
            }

            public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription,
                CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public override Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        public class TestHub : Hub
        {
            public const int IntResult = 42;
            public const string StringResult = "42";
            public const float FloatResult = 42.42f;

            public void VoidMethod() { }
            public Task VoidMethodAsync() { return Task.CompletedTask; }

            public int IntMethod() { return IntResult; }
            public Task<int> IntMethodAsync() { return Task.FromResult(IntResult); }

            public string StringMethod() { return StringResult; }
            public Task<string> StringMethodAsync() { return Task.FromResult(StringResult); }

            public float FloatMethod() { return FloatResult; }
            public Task<float> FloatMethodAsync() { return Task.FromResult(FloatResult); }
        }
    }
}
