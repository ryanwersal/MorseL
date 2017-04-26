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
    public class WebSocketClient
    {
        private readonly WebSocket _internalWebSocket;
        private readonly AsyncProducerConsumerQueue<WebSocketPacket> _packets = new AsyncProducerConsumerQueue<WebSocketPacket>();

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

        public Task ConnectAsync(CancellationToken cts = default(CancellationToken))
        {
            var task = Task.Run(async () =>
            {
                Exception exception = null;
                _internalWebSocket.Error += (sender, args) => exception = args.Exception;
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

                await connectTask;

                if (exception != null)
                {
                    throw exception;
                }
            }, cts);
            task.ConfigureAwait(false);

            return task;
        }

        public Task CloseAsync(CancellationToken cts = default(CancellationToken))
        {
            var task = Task.Run(async () =>
            {
                if (_internalWebSocket.State != WebSocketState.Open)
                {
                    throw new WebSocketClientException("The socket isn't open.");
                }

                Exception exception = null;
                _internalWebSocket.Error += (sender, args) => exception = args.Exception;

                var closeTask = Task.Run(async () =>
                {
                    _internalWebSocket.Close();

                    while (!cts.IsCancellationRequested
                           && (_internalWebSocket.State == WebSocketState.Open
                               || _internalWebSocket.State == WebSocketState.Closing))
                    {
                        await Task.Delay(100, cts);
                    }
                }, cts);

                await closeTask;

                if (exception != null)
                {
                    throw exception;
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
    }

    public class WebSocketClientException : Exception
    {
        public WebSocketClientException(string message) : base(message) { }
    }
}
