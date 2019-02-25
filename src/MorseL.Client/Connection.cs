using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MorseL.Client.Middleware;
using MorseL.Common;
using MorseL.Common.Serialization;
using MorseL.Common.WebSockets.Extensions;
using MorseL.Common.WebSockets.Exceptions;
using MorseL.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx.Synchronous;
using System.Net.Sockets;

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
    public class Connection : IConnection
    {
        public string ConnectionId { get; set; }

        private readonly ClientWebSocket _clientWebSocket;
        private readonly string _name;
        private readonly ILogger _logger;

        /// <summary>
        /// Receiving is handled by a single async process spinning and reading
        /// so we don't have to worry about stomping over the websocket with
        /// receive requests. Writing, on the other hand, is hand-wavy concurrent,
        /// and actually does need to make sure individual messages are sent
        /// atomically.
        /// </summary>
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);

        private bool _hasStarted;
        private bool _isDisposed;
        private bool _wasClosedInvoked;

        private readonly MorseLOptions _options = new MorseLOptions();

        private int _nextId = 0;

        private readonly TaskCompletionSource<object> _connectionCts = new TaskCompletionSource<object>();
        private volatile Task _receiveLoopTask;

        private readonly object _pendingCallsLock = new object();
        private readonly Dictionary<string, InvocationRequest> _pendingCalls = new Dictionary<string, InvocationRequest>();
        private readonly ConcurrentDictionary<string, InvocationHandler> _handlers = new ConcurrentDictionary<string, InvocationHandler>();

        private readonly List<IMiddleware> _middleware = new List<IMiddleware>();

        public event Action Connected;
        public event Action<Exception> Closed;
        public event Action<Exception> Error;

        private readonly string _uri;
        private readonly IList<Exception> _pendingExceptions = new List<Exception>();

        public bool IsConnected => _clientWebSocket.State == WebSocketState.Open;

        public Connection(string uri, string name = null, Action<MorseLOptions> options = null, Action<ClientWebSocketOptions> webSocketOptions = null, ILogger logger = null)
        {
            _name = name;
            _logger = logger ?? NullLogger.Instance;
            _uri = uri;
            options?.Invoke(_options);
            _clientWebSocket = new ClientWebSocket();
            webSocketOptions?.Invoke(_clientWebSocket.Options);

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

            ct.Register(() => _connectionCts.TrySetCanceled(ct));

            await Task.Run(async () =>
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                await _clientWebSocket.ConnectAsync(new Uri(_uri), ct).ConfigureAwait(false);
                stopwatch.Stop();

                _logger.LogDebug($"Connection to {_uri} established after {stopwatch.Elapsed.ToString("mm\\:ss\\.ff")}");

                _receiveLoopTask = Receive(async stream =>
                {
                    _logger.LogTrace($"Received message stream - beginning processing");
                    var message = await MessageSerializer.DeserializeAsync<Message>(stream, Encoding.UTF8).ConfigureAwait(false);
                    _logger.LogTrace($"Received \"{message}\"");

                    switch (message.MessageType)
                    {
                        case MessageType.ConnectionEvent:
                            _connectionCts.TrySetResult(null);

                            ConnectionId = message.Data;
                            Connected?.Invoke();
                            break;

                        case MessageType.ClientMethodInvocation:
                            InvocationDescriptor invocationDescriptor = null;
                            try
                            {
                                invocationDescriptor = MessageSerializer.DeserializeInvocationDescriptor(message.Data, _handlers);
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
                                resultDescriptor = MessageSerializer.DeserializeInvocationResultDescriptor(message.Data, _pendingCalls);
                            }
                            catch (Exception exception)
                            {
                                await HandleInvalidInvocationResultDescriptor(message.Data, exception).ConfigureAwait(false);
                                return;
                            }

                            HandleInvokeResult(resultDescriptor);
                            break;
                        case MessageType.Error:
                            await HandleErrorMessage(message).ConfigureAwait(false);
                            break;
                    }
                }, ct);
                _receiveLoopTask.ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        Error?.Invoke(task.Exception);
                    }
                });

            }, ct).ConfigureAwait(false);

            var timeoutTask = Task.Delay(_options.ConnectionTimeout);
            await Task.WhenAny(_connectionCts.Task, timeoutTask).ConfigureAwait(false);

            if (!ct.IsCancellationRequested && !_connectionCts.Task.IsCompleted && timeoutTask.IsCompleted)
            {
                throw new TimeoutException($"Connection attempt to {_uri} timed out after {_options.ConnectionTimeout}");
            }
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
                invocationDescriptor = MessageSerializer.Deserialize<JObject>(serializedInvocationDescriptor);
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
                await SendMessageAsync(stream => MessageSerializer.WriteObjectToStreamAsync(stream, descriptor));
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
            var stringStream = new StringReader(message);
            using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(message)))
            {
                await SendMessageAsync((stream) => memoryStream.CopyToAsync(stream));
            }
        }

        private async Task SendMessageAsync(Func<Stream, Task> writer)
        {
            var transformIterator = ((IEnumerable<IMiddleware>)_middleware).Reverse().GetEnumerator();
            TransmitDelegate transformDelegator = null;
            transformDelegator = async transformedStream =>
            {
                if (transformIterator.MoveNext())
                {
                    using (_logger?.Tracer($"Middleware[{transformIterator.Current.GetType()}].SendAsync(...)"))
                    {
                        await transformIterator.Current.SendAsync(transformedStream, transformDelegator).ConfigureAwait(false);
                    }
                }
                else
                {
                    await writer(transformedStream).ConfigureAwait(false);
                }
            };

            await _writeLock.WaitAsync();
            try
            {
                using (var writeStream = _clientWebSocket.GetWriteStream())
                {
                    await transformDelegator
                        .Invoke(writeStream)
                        .ContinueWith(async task =>
                        {
                            // Dispose first before handling return state
                            transformIterator.Dispose();

                            // Unwrap and allow the outer exception handler to handle this case
                            task.WaitAndUnwrapException();
                        })
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                _writeLock.Release();
            }
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
            _clientWebSocket.Dispose();
        }

        public async Task DisposeAsync(CancellationToken ct = default(CancellationToken))
        {
            // Stop any connection attempts
            _connectionCts.TrySetCanceled();

            if (_isDisposed) throw new MorseLException("This connection has already been disposed.");
            _isDisposed = true;

            // These states were determined based on error messages thrown when sending
            // CloseAsync in a bad state ¯\_(ツ)_/¯
            if (_clientWebSocket.State == WebSocketState.Open
                || _clientWebSocket.State == WebSocketState.CloseReceived
                || _clientWebSocket.State == WebSocketState.CloseSent)
            {
                // Intentionally ignore all exceptions when closing a socket. We don't care if
                // it is unsuccessful due to an error as they will just timeout on the server
                // eventually.
                try
                {
                    await _clientWebSocket.CloseAsync(WebSocketCloseStatus.Empty, null, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(new EventId(), ex, "Observed exception when closing the socket.");
                }
            }

            _clientWebSocket.Dispose();

            if (_receiveLoopTask != null)
            {
                await _receiveLoopTask;
            }

            // Fire the closed event if necessary
            FireClosed();
        }

        private void FireClosed(Exception e = null)
        {
            if (_wasClosedInvoked) return;
            _wasClosedInvoked = true;
            Closed?.Invoke(e);
        }

        private async Task Receive(Func<Stream, Task> handleMessage, CancellationToken ct)
        {
            Exception closingException = null;

            while (!ct.IsCancellationRequested && _clientWebSocket.State == WebSocketState.Open)
            {
                try
                {
                    ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[1024 * 4]);

                    using (var stream = _clientWebSocket.GetReadStream())
                    {
                        // Wait for data
                        await stream.WaitForDataAsync(ct);

                        var receiveIterator = _middleware.GetEnumerator();
                        RecieveDelegate receiveDelegator = null;
                        receiveDelegator = async transformedMessage =>
                        {
                            if (receiveIterator.MoveNext())
                            {
                                using (_logger?.Tracer($"Middleware[{receiveIterator.Current.GetType()}].RecieveAsync(...)"))
                                {
                                    await receiveIterator.Current.RecieveAsync(new ConnectionContext(_clientWebSocket, transformedMessage.Stream), receiveDelegator).ConfigureAwait(false);
                                }
                            }
                            else
                            {
                                using (_logger?.Tracer("Connection.InternalReceive(...)"))
                                {
                                    await handleMessage(transformedMessage.Stream).ConfigureAwait(false);
                                }
                            }
                        };

                        await receiveDelegator(new ConnectionContext(_clientWebSocket, stream))
                            .ContinueWith(task =>
                            {
                                // Dispose of our middleware iterator before we handle possible exceptions
                                receiveIterator.Dispose();

                                // Unwrap and allow the outer catch to handle if we have an exception
                                task.WaitAndUnwrapException();
                            })
                            .ConfigureAwait(false);
                    }
                }
                catch (WebSocketClosedException e)
                {
                    _logger.LogWarning("Observed WebSocketClosedException - likely OK");

                    // Eat the exception because we're closing
                    closingException = e;

                    try
                    {
                        if (_clientWebSocket.State == WebSocketState.CloseReceived)
                        {
                            // Attempt to be "good" and close
                            await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, ct);
                        }
                    }
                    catch (Exception) { }

                    // Fire the closing event
                    FireClosed(e);

                    // But we cancel out because the socket is closed
                    break;
                }
                catch (Exception e)
                {
                    _logger.LogError(new EventId(), e, $"Observed {e.GetType()}!");

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
                request.Completion.TrySetException(closingException ?? new WebSocketClosedException("The WebSocket has been closed!"));
            }
        }

        private string GetNextId()
        {
            return Interlocked.Increment(ref _nextId).ToString();
        }
    }
}
