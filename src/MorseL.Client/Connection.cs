using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("MorseL.Scaleout.Redis.Tests")]
[assembly: InternalsVisibleTo("MorseL.Tests")]
namespace MorseL.Client
{
    /// <summary>
    /// <para>
    /// High Level Overview
    /// </para>
    /// <para>
    /// <see cref="StartAsync"/> is called to establish and initiate a connection
    /// with a MorseL Hub. This connects the websocket and starts a receive loop
    /// in an un-awaited task.
    /// </para>
    /// <para>
    /// The receive loops, handled by <see cref="Receive(Func{Message, Task})"/>,
    /// checks for inbound data packets on the websocket, and when received, parses
    /// them into <see cref="Message"/> objects and handles them appropriately. As
    /// this loops is executing in an un-awaited task, exceptions do not get bubbled
    /// up. In order to handle exceptions, they are added to
    /// <see cref="_pendingExceptions"/> and an <see cref="AggregateException"/> is
    /// thrown upon the next <see cref="Invoke"/> call.
    /// </para>
    /// </summary>
    public class Connection
    {
        public string ConnectionId { get; set; }

        private readonly WebSocketClient _clientWebSocket;
        private readonly string _name;
        private readonly ILogger _logger;
        private bool _hasStarted;
        private bool _isDisposed;

        private readonly MorseLOptions _options = new MorseLOptions();

        private int _nextId = 0;

        private readonly TaskCompletionSource<object> _taskCompletionSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        private volatile Task _receiveLoopTask;

        private readonly object _pendingCallsLock = new object();
        private readonly Dictionary<string, InvocationRequest> _pendingCalls = new Dictionary<string, InvocationRequest>();
        private readonly ConcurrentDictionary<string, InvocationHandler> _handlers = new ConcurrentDictionary<string, InvocationHandler>();

        private readonly List<IMiddleware> _middleware = new List<IMiddleware>();

        public event Action Connected;
        public event Action<Exception> Closed;
        public event Action<Exception> Error;

        private readonly IList<Exception> _pendingExceptions = new List<Exception>();

        public bool IsConnected => _clientWebSocket.State == WebSocketState.Open;

        public Connection(string uri, string name = null, Action<MorseLOptions> options = null, Action<WebSocketClientOption> config = null, Action<SecurityOption> securityConfig = null, ILogger logger = null)
        {
            _name = name;
            _logger = logger;
            options?.Invoke(_options);
            _clientWebSocket = new WebSocketClient(uri, config, securityConfig);
            _clientWebSocket.Connected += () => Connected?.Invoke();
            _clientWebSocket.Closed += () => Closed?.Invoke(null);

            // Used to track exceptions and rethrow when able
            Error += (exception) =>
            {
                if (_options.RethrowUnobservedExceptions)
                {
                    _pendingExceptions.Add(exception);
                }
            };
        }

        public void AddMiddleware(IMiddleware middleware)
        {
            _middleware.Add(middleware);
        }

        public async Task StartAsync(CancellationToken ct = default(CancellationToken))
        {
            if (_hasStarted) throw new MorseLException($"Cannot call {nameof(StartAsync)} more than once.");
            _hasStarted = true;

            await Task.Run(async () =>
            {
                await _clientWebSocket.ConnectAsync(ct).ConfigureAwait(false);

                _receiveLoopTask = Receive(async message =>
                {
                    switch (message.MessageType)
                    {
                        case MessageType.ConnectionEvent:
                            ConnectionId = message.Data;
                            break;

                        case MessageType.ClientMethodInvocation:
                            InvocationDescriptor invocationDescriptor = null;
                            try
                            {
                                invocationDescriptor = Json.DeserializeInvocationDescriptor(message.Data, _handlers);
                            }
                            catch (Exception exception)
                            {
                                await HandleUnparseableReceivedInvocationDescriptor(message.Data, exception).ConfigureAwait(false);
                                return;
                            }

                            if (invocationDescriptor == null)
                            {
                                // Valid JSON but unparseable into a known, typed invocation descriptor (unknown method name, invalid parameters)
                                await HandleInvalidReceivedInvocationDescriptor(message.Data).ConfigureAwait(false);
                                return;
                            }

                            await InvokeOn(invocationDescriptor).ConfigureAwait(false);
                            break;

                        case MessageType.InvocationResult:
                            InvocationResultDescriptor resultDescriptor;
                            try
                            {
                                resultDescriptor = Json.DeserializeInvocationResultDescriptor(message.Data, _pendingCalls);
                            }
                            catch (Exception exception)
                            {
                                await HandleInvalidInvocationResultDescriptor(message.Data, exception);
                                return;
                            }

                            HandleInvokeResult(resultDescriptor);
                            break;
                        case MessageType.Error:
                            await HandleErrorMessage(message);
                            break;
                    }
                }, ct);
                _receiveLoopTask.ContinueWith(task => {
                    if (task.IsFaulted)
                    {
                        Error?.Invoke(task.Exception);
                    }
                });
            }, ct).ConfigureAwait(false);
        }

        private Task HandleMissingReceivedInvocationDescriptor(JObject invocationDescriptor)
        {
            var invocationId = invocationDescriptor.Value<string>("Id");

            var methodName = invocationDescriptor.Value<string>("MethodName");
            methodName = string.IsNullOrWhiteSpace(methodName) ? "[Invalid Method Name]" : methodName;

            JArray argumentTokenList = invocationDescriptor.Value<JArray>("Arguments");
            var argumentList = argumentTokenList?.Count > 0 ? String.Join(", ", argumentTokenList) : "[No Parameters]";

            return HandleMissingReceivedInvocationDescriptor(invocationId, methodName, argumentList);
        }

        private Task HandleMissingReceivedInvocationDescriptor(InvocationDescriptor invocationDescriptor)
        {
            var methodName = invocationDescriptor.MethodName;
            methodName = string.IsNullOrWhiteSpace(methodName) ? "[Invalid Method Name]" : methodName;

            object[] argumentTokenList = invocationDescriptor.Arguments;
            var argumentList = argumentTokenList?.Length > 0 ? String.Join(", ", argumentTokenList) : "[No Parameters]";

            return HandleMissingReceivedInvocationDescriptor(invocationDescriptor.Id, methodName, argumentList);
        }

        private Task HandleMissingReceivedInvocationDescriptor(string invocationId, string methodName, string argumentList)
        {
            if (_options.ThrowOnMissingMethodRequest)
            {
                throw new MorseLException($"Invalid method request received; method is \"{methodName}({argumentList})\"");
            }

            _logger?.LogDebug($"Invalid method request received; method is \"{methodName}({argumentList})\"");

            // TODO : Move to a Error type that can be handled specifically
            // Since we don't have an invocation descriptor we can't return an invocation result
            return SendMessageAsync($"Error: Cannot find method \"{methodName}({argumentList})\"");
        }

        private Task HandleInvalidReceivedInvocationDescriptor(string serializedInvocationDescriptor)
        {
            // Try to create a typeless descriptor
            JObject invocationDescriptor = null;
            try
            {
                invocationDescriptor = Json.Deserialize<JObject>(serializedInvocationDescriptor);
            }
            catch { }

            // We were able to make heads or tails of the invocation descriptor
            if (invocationDescriptor != null)
            {
                return HandleMissingReceivedInvocationDescriptor(invocationDescriptor);
            }

            return HandleUnparseableReceivedInvocationDescriptor(serializedInvocationDescriptor);
        }

        private Task HandleUnparseableReceivedInvocationDescriptor(string serializedInvocationDescriptor, Exception exception = null)
        {
            // We have no idea what we have for a message
            if (_options.ThrowOnInvalidMessage)
            {
                throw new MorseLException($"Invalid message received \"{serializedInvocationDescriptor}\"", exception);
            }

            _logger?.LogError(new EventId(), exception, $"Invalid message \"{serializedInvocationDescriptor}\"");

            // TODO : Move to a Error type that can be handled specifically
            // Since we don't have an invocation descriptor we can't return an invocation result
            return SendMessageAsync($"Error: Invalid message \"{serializedInvocationDescriptor}\"");
        }

        private Task HandleInvalidInvocationResultDescriptor(string serializedResultDescriptor, Exception exception = null)
        {
            if (_options.ThrowOnInvalidMessage)
            {
                throw new MorseLException($"Invalid result descriptor received \"{serializedResultDescriptor}\"", exception);
            }

            _logger?.LogError(new EventId(), exception, $"Invalid result descriptor \"{serializedResultDescriptor}\"");

            // TODO : Move to a Error type that can be handled specifically
            // Since we don't have an invocation descriptor we can't return an invocation result
            return SendMessageAsync($"Error: Invalid result descriptor \"{serializedResultDescriptor}\"");
        }

        private Task HandleErrorMessage(Message message)
        {
            // We must have sent some bad JSON?
            if (_options.ThrowOnInvalidRequest)
            {
                throw new MorseLException(message.Data);
            }

            _logger?.LogError(new EventId(), message.Data);

            return Task.CompletedTask;
        }

        public void On(string methodName, Type[] types, Action<object[]> handler)
        {
            if (_options.RethrowUnobservedExceptions && _pendingExceptions?.Count > 0)
            {
                throw new AggregateException("Unobserved exceptions thrown during receive loop", _pendingExceptions);
            }

            var invocationHandler = new InvocationHandler(handler, types);
            _handlers.AddOrUpdate(methodName, invocationHandler, (_, __) => invocationHandler);
        }

        public Task Invoke(string methodName, params object[] args) => Invoke<object>(methodName, CancellationToken.None, args);
        public Task<T> Invoke<T>(string methodName, params object[] args) => Invoke<T>(methodName, CancellationToken.None, args);
        public async Task<T> Invoke<T>(string methodName, CancellationToken cancellationToken, params object[] args) => (T)await Invoke(methodName, typeof(T), cancellationToken, args).ConfigureAwait(false);
        public Task<object> Invoke(string methodName, Type returnType, params object[] args) => Invoke(methodName, returnType, CancellationToken.None, args);
        public async Task<object> Invoke(string methodName, Type returnType, CancellationToken cancellationToken, params object[] args)
        {
            if (!IsConnected) throw new MorseLException("Cannot call Invoke when not connected.");

            if (_options.RethrowUnobservedExceptions && _pendingExceptions?.Count > 0)
            {
                throw new AggregateException("Unobserved exceptions thrown during receive loop", _pendingExceptions);
            }

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
                await SendMessageAsync(message);
            }
            catch (Exception e)
            {
                request.Completion.TrySetException(e);
                lock (_pendingCallsLock)
                {
                    _pendingCalls.Remove(descriptor.Id);
                }
            }

            return await request.Completion.Task.ConfigureAwait(false);
        }

        private async Task SendMessageAsync(string message)
        {
            var transformIterator = _middleware.GetEnumerator();
            TransmitDelegate transformDelegator = null;
            transformDelegator = async data =>
            {
                if (transformIterator.MoveNext())
                {
                    using (_logger?.Tracer($"Middleware[{transformIterator.Current.GetType()}].SendAsync(...)"))
                    {
                        await transformIterator.Current.SendAsync(data, transformDelegator).ConfigureAwait(false);
                    }
                }
                else
                {
                    using (_logger?.Tracer("Connection.SendAsync(...)"))
                    {
                        await _clientWebSocket.SendAsync(data, CancellationToken.None).ConfigureAwait(false);
                    }
                }
            };
            await transformDelegator
                .Invoke(message)
                .ContinueWith(task =>
                {
                    // Dispose first before handling return state
                    transformIterator.Dispose();

                    // Unwrap and allow the outer exception handler to handle this case
                    task.WaitAndUnwrapException();
                })
                .ConfigureAwait(false);
        }

        private Task InvokeOn(InvocationDescriptor descriptor)
        {
            if (!_handlers.ContainsKey(descriptor.MethodName))
            {
                return HandleMissingReceivedInvocationDescriptor(descriptor);
            }

            var invocationHandler = _handlers[descriptor.MethodName];

            // This task is unawaited to allow in-bound packet payloads to be
            // handled in parallel. Should these tasks be waited each payload
            // is handled synchronously. This has the added disadvantage of
            // deadlocking a caller who does:
            // 1. Invoke(...) -> Pending Response (Blocks caller context)
            // 2.   <- On(...) callback fired (Blocks receive context)
            //      {
            // 3.     -> Invoke(...) -> Pending Response (Blocks receive context)
            // 4. Deadlock
            // In this scenario, the first Invoke(...) awaits on a
            // TaskCompletionSource who's SetResult is called when an
            // InvocationResultDescriptor comes in through the socket recieve
            // loop. When a message for an On callback, not the result descriptor,
            // comes in, the callback is fired allowing a caller to issue another
            // Invoke(...) call which again blocks on another
            // TaskCompletionSource, disallowing the On callback from concluding
            // which prevents the socket receive loop from moving to the
            // next message.

            /*! Unawaited Task !*/
            Task.Run(() => {
                try
                {
                    invocationHandler.Handler(descriptor.Arguments);
                }
                catch (Exception e)
                {
                    _logger?.LogError(new EventId(), e, $"Exception thrown during registered callback for {descriptor.MethodName}");
                    Error?.Invoke(new Exception($"Exception thrown during registered callback for {descriptor.MethodName}", e));
                }
            }).ConfigureAwait(false);

            return Task.CompletedTask;
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
                request.Completion.TrySetException(new MorseLException(descriptor.Error));
            }
            else
            {
                request.Completion.TrySetResult(descriptor.Result);
            }
        }

        internal void KillConnection()
        {
            _clientWebSocket.Dispose(false);
        }

        public async Task DisposeAsync(CancellationToken ct = default(CancellationToken))
        {
            if (_isDisposed) throw new MorseLException("This connection has already been disposed.");
            _isDisposed = true;

            if (_clientWebSocket.State != WebSocketState.Closed)
            {
                await _clientWebSocket.CloseAsync(ct).ConfigureAwait(false);
            }

            _clientWebSocket.Dispose();

            if (_receiveLoopTask != null)
            {
                await _receiveLoopTask;
            }
        }

        private async Task Receive(Func<Message, Task> handleMessage, CancellationToken ct)
        {
            Exception closingException = null;

            while (!ct.IsCancellationRequested && _clientWebSocket.State == WebSocketState.Open)
            {
                try
                {
                    var receivedMessage = await _clientWebSocket.RecieveAsync(ct).ConfigureAwait(false);

                    var receiveIterator = _middleware.GetEnumerator();
                    RecieveDelegate receiveDelegator = null;
                    receiveDelegator = async transformedMessage =>
                    {
                        if (receiveIterator.MoveNext())
                        {
                            using (_logger?.Tracer($"Middleware[{receiveIterator.Current.GetType()}].RecieveAsync(...)"))
                            {
                                await receiveIterator.Current.RecieveAsync(transformedMessage, receiveDelegator).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            using (_logger?.Tracer("Connection.InternalReceive(...)"))
                            {
                                await InternalReceive(transformedMessage, handleMessage).ConfigureAwait(false);
                            }
                        }
                    };

                    await receiveDelegator(receivedMessage)
                        .ContinueWith(task =>
                        {
                            // Dispose of our middleware iterator before we handle possible exceptions
                            receiveIterator.Dispose();

                            // Unwrap and allow the outer catch to handle if we have an exception
                            task.WaitAndUnwrapException();
                        })
                        .ConfigureAwait(false);
                }
                catch (WebSocketClosedException e)
                {
                    // Eat the exception because we're closing
                    closingException = e;

                    // But we cancel out because the socket is closed
                    break;
                }
                catch (Exception e)
                {
                    Error?.Invoke(e);
                }
            }

            // We're closing so we need to unblock any pending TaskCompletionSources
            InvocationRequest[] calls;

            lock (_pendingCallsLock)
            {
                calls = _pendingCalls.Values.ToArray();
                _pendingCalls.Clear();
            }

            foreach (var request in calls)
            {
                request.Registration.Dispose();
                request.Completion.TrySetException(closingException ?? new WebSocketClosedException("The websocket has been closed!"));
            }
        }

        private async Task InternalReceive(WebSocketPacket receivedMessage, Func<Message, Task> handleMessage)
        {
            switch (receivedMessage.MessageType)
            {
                case WebSocketMessageType.Binary:
                    // TODO: Implement.
                    throw new NotImplementedException("Binary messages not supported.");

                case WebSocketMessageType.Text:
                    var serializedMessage = Encoding.UTF8.GetString(receivedMessage.Data);
                    var message = Json.Deserialize<Message>(serializedMessage);
                    await handleMessage(message).ConfigureAwait(false);
                    break;

                case WebSocketMessageType.Close:
                    await _clientWebSocket.CloseAsync(CancellationToken.None).ConfigureAwait(false);
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
