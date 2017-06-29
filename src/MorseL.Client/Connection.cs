using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MorseL.Client.WebSockets;
using Microsoft.Extensions.Logging;
using MorseL.Client.Middleware;
using Nito.AsyncEx.Synchronous;
using SuperSocket.ClientEngine;
using MorseL.Common;
using MorseL.Common.Serialization;
using MorseL.Diagnostics;
using WebSocketMessageType = MorseL.Client.WebSockets.WebSocketMessageType;
using WebSocketState = WebSocket4Net.WebSocketState;

namespace MorseL.Client
{
    public class Connection
    {
        public string ConnectionId { get; set; }

        private WebSocketClient _clientWebSocket { get; }
        private string Name { get; }
        private ILogger _logger;

        private int _nextId = 0;

        private readonly TaskCompletionSource<object> _taskCompletionSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        private volatile Task _receiveLoopTask;

        private readonly object _pendingCallsLock = new object();
        private readonly Dictionary<string, InvocationRequest> _pendingCalls = new Dictionary<string, InvocationRequest>();
        private readonly ConcurrentDictionary<string, InvocationHandler> _handlers = new ConcurrentDictionary<string, InvocationHandler>();

        private readonly List<IMiddleware> _middleware = new List<IMiddleware>();

        public event Action Connected;
        public event Action<Exception> Closed;

        public Connection(string uri, string name = null, Action<WebSocketClientOption> config = null, Action<SecurityOption> securityConfig = null, ILogger logger = null)
        {
            Name = name;
            _logger = logger;
            _clientWebSocket = new WebSocketClient(uri, config, securityConfig);
            _clientWebSocket.Connected += () => Connected?.Invoke();
            _clientWebSocket.Closed += () => Closed?.Invoke(null);
        }

        public void AddMiddleware(IMiddleware middleware)
        {
            _middleware.Add(middleware);
        }

        public Task StartAsync()
        {
            Task.Run(async () =>
            {
                await _clientWebSocket.ConnectAsync().ConfigureAwait(false);

                _receiveLoopTask = Receive(message =>
                {
                    switch (message.MessageType)
                    {
                        case MessageType.ConnectionEvent:
                            ConnectionId = message.Data;
                            break;

                        case MessageType.ClientMethodInvocation:
                            var invocationDescriptor = Json.DeserializeInvocationDescriptor(message.Data, _handlers);
                            if (invocationDescriptor == null)
                            {
                                _logger.LogDebug("Invocation request for unknown hub method");
                                return;
                            }
                            InvokeOn(invocationDescriptor);
                            break;

                        case MessageType.InvocationResult:
                            var resultDescriptor = Json.DeserializeInvocationResultDescriptor(message.Data,
                                _pendingCalls);
                            HandleInvokeResult(resultDescriptor);
                            break;
                    }
                });
                _receiveLoopTask.ContinueWith(task => task.WaitAndUnwrapException());

            }).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    _taskCompletionSource.SetException(task.Exception.InnerException);
                }
                else if (task.IsCanceled)
                {
                    _taskCompletionSource.SetCanceled();
                }
                else
                {
                    _taskCompletionSource.SetResult(null);
                }
            });

            return _taskCompletionSource.Task;
        }

        public void On(string methodName, Type[] types, Action<object[]> handler)
        {
            var invocationHandler = new InvocationHandler(handler, types);
            _handlers.AddOrUpdate(methodName, invocationHandler, (_, __) => invocationHandler);
        }

        public Task Invoke(string methodName, params object[] args) => Invoke<object>(methodName, CancellationToken.None, args);
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

                var transformIterator = _middleware.GetEnumerator();
                TransmitDelegate transformDelegator = null;
                transformDelegator = async data =>
                {
                    if (transformIterator.MoveNext())
                    {
                        using (_logger.Tracer($"Middleware[{transformIterator.Current.GetType()}].SendAsync(...)"))
                        {
                            await transformIterator.Current.SendAsync(data, transformDelegator).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        using (_logger.Tracer("Connection.SendAsync(...)"))
                        {
                            await _clientWebSocket.SendAsync(data, CancellationToken.None).ConfigureAwait(false);
                        }
                    }
                };
                await transformDelegator
                    .Invoke(message)
                    .ContinueWith(task => transformIterator.Dispose())
                    .ConfigureAwait(false);
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
            await _clientWebSocket.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            _clientWebSocket.Dispose();

            if (_receiveLoopTask != null)
            {
                await _receiveLoopTask;
            }
        }

        private async Task Receive(Action<Message> handleMessage)
        {
            try
            {
                while (_clientWebSocket.State == WebSocketState.Open)
                {
                    var receivedMessage = await _clientWebSocket.RecieveAsync(CancellationToken.None).ConfigureAwait(false);

                    var receiveIterator = _middleware.GetEnumerator();
                    RecieveDelegate receiveDelegator = null;
                    receiveDelegator = async transformedMessage =>
                    {
                        if (receiveIterator.MoveNext())
                        {
                            using (_logger.Tracer($"Middleware[{receiveIterator.Current.GetType()}].RecieveAsync(...)"))
                            {
                                await receiveIterator.Current.RecieveAsync(transformedMessage, receiveDelegator).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            using (_logger.Tracer("Connection.InternalReceive(...)"))
                            {
                                await InternalReceive(transformedMessage, handleMessage).ConfigureAwait(false);
                            }
                        }
                    };

                    await receiveDelegator(receivedMessage)
                        .ContinueWith(task => receiveIterator.Dispose())
                        .ConfigureAwait(false);
                }
            }
            catch (WebSocketClosedException)
            {
                // Eat the exception because we're closing
            }
        }

        private async Task InternalReceive(WebSocketPacket receivedMessage, Action<Message> handleMessage)
        {
            switch (receivedMessage.MessageType)
            {
                case WebSocketMessageType.Binary:
                    // TODO: Implement.
                    throw new NotImplementedException("Binary messages not supported.");

                case WebSocketMessageType.Text:
                    var serializedMessage = Encoding.UTF8.GetString(receivedMessage.Data);
                    var message = Json.Deserialize<Message>(serializedMessage);
                    handleMessage(message);
                    break;

                case WebSocketMessageType.Close:
                    await _clientWebSocket
                        .CloseAsync(CancellationToken.None)
                        .ConfigureAwait(false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private string GetNextId()
        {
            return Interlocked.Increment(ref _nextId).ToString();
        }
    }
}