using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using WebSocketManager.Common;
using WebSocketManager.Common.Serialization;

namespace WebSocketManager.Client
{
    public class Connection
    {
        public string ConnectionId { get; set; }

        private ClientWebSocket _clientWebSocket { get; }
        private string Name { get; }

        private int _nextId = 0;

        private readonly object _pendingCallsLock = new object();
        private readonly Dictionary<string, InvocationRequest> _pendingCalls = new Dictionary<string, InvocationRequest>();
        private readonly ConcurrentDictionary<string, InvocationHandler> _handlers = new ConcurrentDictionary<string, InvocationHandler>();

        public event Action Connected;
        public event Action<Exception> Closed;

        public Connection(string name = null, Action<ClientWebSocketOptions> optionConfigAction = null)
        {
            Name = name;
            _clientWebSocket = new ClientWebSocket();
            optionConfigAction?.Invoke(_clientWebSocket.Options);
        }

        public async Task StartAsync(Uri uri)
        {
            await _clientWebSocket.ConnectAsync(uri, CancellationToken.None).ConfigureAwait(false);

            Connected?.Invoke();

            Receive(message =>
            {
                switch (message.MessageType)
                {
                    case MessageType.ConnectionEvent:
                        ConnectionId = message.Data;
                        break;

                    case MessageType.ClientMethodInvocation:
                        var invocationDescriptor = Json.DeserializeInvocationDescriptor(message.Data, _handlers);
                        InvokeOn(invocationDescriptor);
                        break;

                    case MessageType.InvocationResult:
                        var resultDescriptor = Json.DeserializeInvocationResultDescriptor(message.Data, _pendingCalls);
                        HandleInvokeResult(resultDescriptor);
                        break;
                }
            }).ConfigureAwait(false);
        }

        public void On(string methodName, Type[] types, Action<object[]> handler)
        {
            var invocationHandler = new InvocationHandler(handler, types);
            _handlers.AddOrUpdate(methodName, invocationHandler, (_, __) => invocationHandler);
        }

        public Task<T> Invoke<T>(string methodName, params object[] args) => Invoke<T>(methodName, CancellationToken.None, args);
        public async Task<T> Invoke<T>(string methodName, CancellationToken cancellationToken, params object[] args) => (T)await Invoke(methodName, typeof(T), cancellationToken, args);
        public Task<object> Invoke(string methodName, Type returnType, params object[] args) => Invoke(methodName, returnType, CancellationToken.None, args);
        public async Task<object> Invoke(string methodName, Type returnType, CancellationToken cancellationToken, params object[] args)
        {
            var descriptor = new InvocationDescriptor
            {
                Id = GetNextId(),
                MethodName = methodName,
                Arguments = args
            };

            var request = new InvocationRequest(cancellationToken, returnType);

            lock (_pendingCallsLock)
            {
                _pendingCalls.Add(descriptor.Id, request);
            }

            try
            {
                var message = Json.SerializeObject(descriptor);
                await _clientWebSocket.SendAllAsync(message, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                request.Completion.TrySetException(e);
                lock (_pendingCallsLock)
                {
                    _pendingCalls.Remove(descriptor.Id);
                }
            }

            return await request.Completion.Task;
        }

        private void InvokeOn(InvocationDescriptor descriptor)
        {
            var invocationHandler = _handlers[descriptor.MethodName];
            Task.Run(() => invocationHandler.Handler(descriptor.Arguments));
        }

        private void HandleInvokeResult(InvocationResultDescriptor descriptor)
        {
            InvocationRequest request;
            lock (_pendingCallsLock)
            {
                request = _pendingCalls[descriptor.Id];
                _pendingCalls.Remove(descriptor.Id);
            }

            request.Registration.Dispose();

            if (!string.IsNullOrEmpty(descriptor.Error))
            {
                request.Completion.TrySetException(new Exception(descriptor.Error));
            }
            else
            {
                request.Completion.TrySetResult(descriptor.Result);
            }
        }

        public async Task DisposeAsync()
        {
            if (_clientWebSocket.State != WebSocketState.Open) return;
            await _clientWebSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).ConfigureAwait(false);
        }

        private async Task Receive(Action<Message> handleMessage)
        {
            while (_clientWebSocket.State == WebSocketState.Open)
            {
                using (var receivedMessage = await _clientWebSocket.ReceiveAllAsync(CancellationToken.None)
                    .ConfigureAwait(false))
                {
                    switch (receivedMessage.MessageType)
                    {
                        case WebSocketMessageType.Binary:
                            // TODO: Implement.
                            throw new NotImplementedException("Binary messages not supported.");

                        case WebSocketMessageType.Text:
                            var serializedMessage = await receivedMessage.ToStringAsync().ConfigureAwait(false);
                            var message = Json.Deserialize<Message>(serializedMessage);
                            handleMessage(message);
                            break;

                        case WebSocketMessageType.Close:
                            await _clientWebSocket
                                .CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None)
                                .ConfigureAwait(false);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
        }

        private string GetNextId()
        {
            return Interlocked.Increment(ref _nextId).ToString();
        }
    }
}