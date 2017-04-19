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

namespace AsyncWebSocketClient
{
    public class WebSocketClient
    {
        private readonly WebSocket _internalWebSocket;
        private readonly AsyncProducerConsumerQueue<WebSocketPacket> _packets = new AsyncProducerConsumerQueue<WebSocketPacket>();

        public TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(5);
        public TimeSpan DisconnectionTimeout = TimeSpan.FromSeconds(5);

        public WebSocketState State => _internalWebSocket?.State ?? WebSocketState.None;

        public WebSocketClient(string uri, string subProtocol = "", List<KeyValuePair<string, string>> cookies = null, List<KeyValuePair<string, string>> customHeaderItems = null, string userAgent = "", string origin = "", WebSocketVersion version = WebSocketVersion.None, EndPoint httpConnectProxy = null, SslProtocols sslProtocols = SslProtocols.None, int receiveBufferSize = 0, Action<SecurityOption> securityConfig = null)
        {
            _internalWebSocket = new WebSocket(uri, subProtocol, cookies, customHeaderItems, userAgent, origin, version, httpConnectProxy, sslProtocols, receiveBufferSize);
            securityConfig?.Invoke(_internalWebSocket.Security);
        }

        public Task ConnectAsync(CancellationToken cts = default(CancellationToken))
        {
            var task = Task.Run(async () =>
            {
                Exception exception = null;
                
                _internalWebSocket.Error += (sender, args) =>
                {
                    exception = args.Exception;
                };
                _internalWebSocket.DataReceived += async (sender, args) =>
                {
                    await _packets.EnqueueAsync(new WebSocketPacket(WebSocketMessageType.Binary, args.Data), cts);
                };
                _internalWebSocket.MessageReceived += async (sender, args) =>
                {
                    await _packets.EnqueueAsync(new WebSocketPacket(WebSocketMessageType.Text, Encoding.UTF8.GetBytes(args.Message)), cts);
                };

                var connectTask = Task.Run(async () =>
                {
                    _internalWebSocket.Open();
                    while (!cts.IsCancellationRequested
                           && (_internalWebSocket.State == WebSocketState.None
                               || _internalWebSocket.State == WebSocketState.Connecting))
                    {
                        await Task.Delay(100, cts);
                    }
                }, cts);

                await Task.WhenAny(connectTask, Task.Delay(DisconnectionTimeout, cts)).ConfigureAwait(false);

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

        public Task DisconnectAsync(CancellationToken cts = default(CancellationToken))
        {
            var task = Task.Run(async () =>
            {
                _internalWebSocket.Close();

                var closeTask = Task.Run(async () =>
                {
                    while (!cts.IsCancellationRequested
                           && (_internalWebSocket.State == WebSocketState.Open
                               || _internalWebSocket.State == WebSocketState.Closing))
                    {
                        await Task.Delay(100, cts);
                    }
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
            var task = Task.Run(() => _internalWebSocket.Send(message), cts);
            task.ConfigureAwait(false);

            return task;
        }

        public Task SendAsync(ArraySegment<byte> buffer, CancellationToken cts = default(CancellationToken))
        {
            var task = Task.Run(() => _internalWebSocket.Send(new[] { buffer }), cts);
            task.ConfigureAwait(false);

            return task;
        }

        public Task<WebSocketPacket> RecieveAsync(CancellationToken cts = default(CancellationToken))
        {
            var task = Task.Run(async () => await _packets.DequeueAsync(cts), cts);
            task.ConfigureAwait(false);

            return task;
        }
    }

    public class WebSocketClientException : Exception
    {
        public WebSocketClientException(string message) : base(message) { }
    }
}
