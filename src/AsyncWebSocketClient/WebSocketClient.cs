using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SuperSocket.ClientEngine;
using WebSocket4Net;

namespace AsyncWebSocketClient
{
    public class WebSocketClient
    {
        private WebSocket _internalWebSocket;
        private SecurityOption _security;
        private Queue<WebSocketPacket> _packets = new Queue<WebSocketPacket>();
        private ReaderWriterLockSlim _packetLock = new ReaderWriterLockSlim();

        public TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(5);

        public WebSocketState State => _internalWebSocket?.State ?? WebSocketState.None;

        public SecurityOption Security
        {
            get
            {
                if (_internalWebSocket != null)
                {
                    return _internalWebSocket.Security;
                }

                return _security;
            }
            set
            {
                if (_internalWebSocket != null)
                {
                    throw new WebSocketClientException("Cannot be modified after ConnectAsync has been called.");
                }

                _security = value;
            }
        }

        public WebSocketClient() : this(new SecurityOption())
        {
        }

        public WebSocketClient(SecurityOption security)
        {
            _security = security;
        }

        public Task ConnectAsync(string uri, string subProtocol = "", List<KeyValuePair<string, string>> cookies = null, List<KeyValuePair<string, string>> customHeaderItems = null, string userAgent = "", string origin = "", WebSocketVersion version = WebSocketVersion.None, EndPoint httpConnectProxy = null, SslProtocols sslProtocols = SslProtocols.None, int receiveBufferSize = 0, CancellationToken cts = default(CancellationToken))
        {
            var task = Task.Run(async () =>
            {
                Exception exception = null;

                _internalWebSocket = new WebSocket(uri, subProtocol, cookies, customHeaderItems, userAgent, origin, version, httpConnectProxy, sslProtocols, receiveBufferSize);
                _internalWebSocket.Error += (sender, args) =>
                {
                    exception = args.Exception;
                };
                _internalWebSocket.DataReceived += (sender, args) =>
                {
                    _packetLock.EnterWriteLock();
                        _packets.Enqueue(new WebSocketPacket(WebSocketMessageType.Binary, args.Data));
                    _packetLock.ExitWriteLock();
                };
                _internalWebSocket.MessageReceived += (sender, args) =>
                {
                    _packetLock.EnterWriteLock();
                        _packets.Enqueue(new WebSocketPacket(WebSocketMessageType.Text, Encoding.UTF8.GetBytes(args.Message)));
                    _packetLock.ExitWriteLock();
                };

                var connectTask = Task.Run(async () =>
                {
                    _internalWebSocket.Open();
                    while (_internalWebSocket.State == WebSocketState.None || _internalWebSocket.State == WebSocketState.Connecting) { await Task.Delay(100, cts); }
                }, cts);

                await Task.WhenAny(connectTask, Task.Delay(ConnectionTimeout, cts)).ConfigureAwait(false);

                if (!connectTask.IsCompleted)
                {
                    throw new TimeoutException();
                }

                if (exception != null)
                {
                    throw exception;
                }
            }, cts);
            task.ConfigureAwait(false);

            return task;
        }

        public Task DisconnectAsync(CancellationToken cts)
        {
            var task = Task.Run(async () =>
            {
                _internalWebSocket.Close();

                var closeTask = Task.Run(async () =>
                {
                    while (_internalWebSocket.State == WebSocketState.Open || _internalWebSocket.State == WebSocketState.Closing) { await Task.Delay(100, cts); }
                }, cts);

                await Task.WhenAny(closeTask, Task.Delay(ConnectionTimeout, cts)).ConfigureAwait(false);

                if (!closeTask.IsCompleted)
                {
                    throw new TimeoutException();
                }
            }, cts);
            task.ConfigureAwait(false);

            return task;
        }

        public Task SendAsync(string message, CancellationToken cts = default(CancellationToken))
        {
            var task = Task.Run(() =>
            {
                _internalWebSocket.Send(message);
            }, cts);
            task.ConfigureAwait(false);

            return task;
        }

        public Task SendAsync(ArraySegment<byte> buffer, CancellationToken cts = default(CancellationToken))
        {
            var task = Task.Run(() =>
            {
                _internalWebSocket.Send(new[] { buffer });
            }, cts);
            task.ConfigureAwait(false);

            return task;
        }

        public Task<WebSocketPacket> RecieveAsync(CancellationToken cts = default(CancellationToken))
        {
            var task = Task.Run(async () =>
            {
                WebSocketPacket packet;
                while (true)
                {
                    while (_packets.Count == 0) { await Task.Delay(100, cts); }

                    _packetLock.EnterWriteLock();
                    try
                    {
                        if (_packets.Count == 0) continue;
                        packet = _packets.Dequeue();
                        break;
                    }
                    finally
                    {
                        _packetLock.ExitWriteLock();
                    }
                }
                return packet;
            }, cts);
            task.ConfigureAwait(false);

            return task;
        }
    }

    public class WebSocketClientException : Exception
    {
        public WebSocketClientException(string message) : base(message) { }
    }
}
