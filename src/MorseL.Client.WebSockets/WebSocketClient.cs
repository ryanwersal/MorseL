using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using SuperSocket.ClientEngine;
using WebSocket4Net;

namespace MorseL.Client.WebSockets
{
    public class WebSocketClient : IDisposable
    {
        private readonly WebSocket _internalWebSocket;
        private readonly AsyncProducerConsumerQueue<WebSocketPacket> _packets = new AsyncProducerConsumerQueue<WebSocketPacket>();

        /// Internal CancellationTokenSource used to stop:
        /// - Connecting
        /// - Sending
        /// - Receiving
        /// - NOT Closing
        private readonly CancellationTokenSource _internalCts = new CancellationTokenSource();

        public WebSocketClientOption Options { get; }
        public SecurityOption Security { get; }
        public WebSocketState State => _internalWebSocket?.State ?? WebSocketState.None;
        public Action Connected;
        public Action Closed;
        public Action<Exception> Error;

        public WebSocketClient(string uri, Action<WebSocketClientOption> config=null, Action<SecurityOption> securityConfig=null)
        {
            Options = new WebSocketClientOption();
            config?.Invoke(Options);
            _internalWebSocket = new WebSocket(uri,
                Options.SubProtocol,
                Options.Cookies,
                Options.CustomHeaderItems,
                Options.UserAgent,
                Options.Origin,
                Options.Version,
                Options.HttpConnectProxy,
                Options.SslProtocols,
                Options.ReceiveBufferSize);
            Security = _internalWebSocket.Security;
            securityConfig?.Invoke(Security);
            _internalWebSocket.EnableAutoSendPing = Options.EnableAutoSendPing;
            _internalWebSocket.AutoSendPingInterval = Options.AutoSendPingIntervalSeconds;

            _internalWebSocket.Opened += (sender, args) =>
            {
                Connected?.Invoke();
            };
            _internalWebSocket.Closed += (sender, args) =>
            {
                Closed?.Invoke();
            };
            _internalWebSocket.Error += (sender, args) =>
            {
                Error?.Invoke(args.Exception);
            };
        }

        public Task ConnectAsync(CancellationToken cancelToken = default(CancellationToken))
        {
            // Create a linked cancellation token
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_internalCts.Token, cancelToken);

            var task = Task.Run(async () =>
            {
                Exception exception = null;
                // Async handlers use the internal token only
                _internalWebSocket.Error += (sender, args) => exception = args.Exception;
                _internalWebSocket.DataReceived += async (sender, args) =>
                {
                    await _packets.EnqueueAsync(new WebSocketPacket(WebSocketMessageType.Binary, args.Data), _internalCts.Token);
                };
                _internalWebSocket.MessageReceived += async (sender, args) =>
                {
                    await _packets.EnqueueAsync(new WebSocketPacket(WebSocketMessageType.Text, Encoding.UTF8.GetBytes(args.Message)), _internalCts.Token);
                };

                var connectTask = Task.Run(async () =>
                {
                    _internalWebSocket.Open();
                    while (!linkedCts.IsCancellationRequested
                           && (_internalWebSocket.State == WebSocketState.None || _internalWebSocket.State == WebSocketState.Connecting))
                    {
                        await Task.Delay(100, linkedCts.Token);
                    }
                }, linkedCts.Token);

                await connectTask;

                if (exception != null)
                {
                    throw exception;
                }
            }, linkedCts.Token);

            // Don't pass the external cancel token so we make sure the linked gets disposed
            task.ContinueWith(t =>
            {
                linkedCts.Dispose();

                if (cancelToken.IsCancellationRequested)
                {
                    cancelToken.ThrowIfCancellationRequested();
                }
            });
            task.ConfigureAwait(false);

            return task;
        }

        public Task CloseAsync(CancellationToken cancelToken = default(CancellationToken))
        {
            // We don't link to the internal cancel token here as we wouldn't want to stop ourselves...
            var task = Task.Run(async () =>
            {
                if (_internalWebSocket.State != WebSocketState.Open)
                {
                    throw new WebSocketClientException("The socket isn't open.");
                }

                // Cancel any internal operations
                _internalCts.Cancel();

                Exception exception = null;
                _internalWebSocket.Error += (sender, args) => exception = args.Exception;

                // Issue the internal close request and wait until the socket closes
                var closeTask = Task.Run(async () =>
                {
                    _internalWebSocket.Close();

                    while (!cancelToken.IsCancellationRequested
                           && (_internalWebSocket.State == WebSocketState.Open || _internalWebSocket.State == WebSocketState.Closing))
                    {
                        await Task.Delay(100, cancelToken);
                    }
                }, cancelToken);

                // Join the task
                await closeTask;

                // Kill the packet queue
                _packets.CompleteAdding();

                if (exception != null)
                {
                    throw exception;
                }
            }, cancelToken);
            task.ContinueWith(t =>
            {
                if (cancelToken.IsCancellationRequested)
                {
                    cancelToken.ThrowIfCancellationRequested();
                }
            });
            task.ConfigureAwait(false);

            return task;
        }

        public Task SendAsync(string message, CancellationToken cancelToken = default(CancellationToken))
        {
            // Create a linked cancellation token
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_internalCts.Token, cancelToken);

            var task = Task.Run(() => _internalWebSocket.Send(message), linkedCts.Token);

            // Don't pass the external cancel token so we make sure the linked gets disposed
            task.ContinueWith(t =>
            {
                linkedCts.Dispose();

                if (cancelToken.IsCancellationRequested)
                {
                    cancelToken.ThrowIfCancellationRequested();
                }
            });
            task.ConfigureAwait(false);

            return task;
        }

        public Task SendAsync(ArraySegment<byte> buffer, CancellationToken cancelToken = default(CancellationToken))
        {
            // Create a linked cancellation token
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_internalCts.Token, cancelToken);

            var task = Task.Run(() => _internalWebSocket.Send(new[] { buffer }), linkedCts.Token);

            // Don't pass the external cancel token so we make sure the linked gets disposed
            task.ContinueWith(t =>
            {
                linkedCts.Dispose();

                if (cancelToken.IsCancellationRequested)
                {
                    cancelToken.ThrowIfCancellationRequested();
                }
            });
            task.ConfigureAwait(false);

            return task;
        }

        public Task<WebSocketPacket> RecieveAsync(CancellationToken cancelToken = default(CancellationToken))
        {
            // Create a linked cancellation token
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_internalCts.Token, cancelToken);

            var task = Task.Run(async () =>
            {
                try
                {
                    return await _packets.DequeueAsync(linkedCts.Token);
                }
                catch (Exception exception)
                {
                    // Catch if we were cancelled or stopped (packet queue closed)
                    if (exception is OperationCanceledException || exception is InvalidOperationException)
                    {
                        // We eat this exception if we're being closed and throw a WebSocketClosed exception
                        if (_internalCts.IsCancellationRequested)
                        {
                            throw new WebSocketClosedException($"WebSocket is closing");
                        }
                    }

                    // Rethrow otherwise (???)
                    throw;
                }
            // Only the cancellation token here so we can handle the internal exceptions
            }, cancelToken);

            // Don't pass the external cancel token so we make sure the linked gets disposed
            task.ContinueWith(t =>
            {
                linkedCts.Dispose();

                if (cancelToken.IsCancellationRequested)
                {
                    cancelToken.ThrowIfCancellationRequested();
                }
            });
            task.ConfigureAwait(false);

            return task;
        }

        public void Dispose()
        {
            if (_internalWebSocket.State == WebSocketState.Open)
            {
                CloseAsync(CancellationToken.None).Wait();
            }

            _internalCts.Dispose();
            _internalWebSocket?.Dispose();
            _packets.CompleteAdding();

            Connected = null;
            Closed = null;
            Error = null;
        }
    }

    public class WebSocketClientOption
    {
        public string SubProtocol { get; set; } = "";
        public List<KeyValuePair<string, string>> Cookies { get; set; } = null;
        public List<KeyValuePair<string, string>> CustomHeaderItems { get; set; } = null;
        public string UserAgent { get; set; } = "";
        public string Origin { get; set; } = "";
        public WebSocketVersion Version { get; set; } = WebSocketVersion.None;
        public EndPoint HttpConnectProxy { get; set; } = null;
        public SslProtocols SslProtocols { get; set; } = SslProtocols.None;
        public int ReceiveBufferSize { get; set; } = 0;
        public bool EnableAutoSendPing { get; set; } = false;
        public int AutoSendPingIntervalSeconds { get; set; } = 120;
    }

    public class WebSocketClientException : Exception
    {
        public WebSocketClientException(string message) : base(message) { }
    }

    public class WebSocketClosedException : WebSocketClientException
    {
        public WebSocketClosedException(string message) : base(message) { }
    }
}
