using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MorseL.Sockets.Middleware;

namespace MorseL.Sockets
{
    public class WebSocketChannel : IChannel
    {
        internal readonly WebSocket Socket;
        internal Connection Connection;
        internal IEnumerable<IMiddleware> Middleware;
        internal ILogger Logger;
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1);

        public WebSocketChannel(WebSocket socket, IEnumerable<IMiddleware> middleware, ILoggerFactory loggerFactory)
        {
            Socket = socket;
            Middleware = middleware ?? new List<IMiddleware>();
            Logger = loggerFactory.CreateLogger<WebSocketChannel>();
        }

        public ChannelState State => Socket?.State != null ? (ChannelState) Socket.State : ChannelState.None;

        public async Task SendAsync(Stream stream)
        {
            var context = new ConnectionContext(Connection, stream);
            var iterator = Middleware.GetEnumerator();
            MiddlewareDelegate delegator = BuildMiddlewareDelegate(iterator, InternalSendAsync);

            await delegator
                .Invoke(context)
                .ContinueWith(task => iterator.Dispose())
                .ConfigureAwait(false);
        }

        internal MiddlewareDelegate BuildMiddlewareDelegate(IEnumerator<IMiddleware> iterator, Func<Stream, Task> internalSend)
        {
            MiddlewareDelegate delegator = null;
            delegator = async transformedContext =>
            {
                if (iterator.MoveNext())
                {
                    await iterator.Current.SendAsync(transformedContext, delegator).ConfigureAwait(false);
                }
                else
                {
                    await internalSend(transformedContext.Stream).ConfigureAwait(false);
                }
            };

            return delegator;
        }

        private async Task InternalSendAsync(Stream stream)
        {
            var buffer = new byte[8000];

            await _writeLock.WaitAsync().ConfigureAwait(false);

            try
            {
                int count;
                do
                {
                    count = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                    await Socket.SendAsync(
                            new ArraySegment<byte>(buffer, 0, count),
                            WebSocketMessageType.Text,
                            count < buffer.Length,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                } while (count == buffer.Length);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task DisposeAsync()
        {
            if (Socket == null) return;

            try
            {
                await Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            }
            catch (WebSocketException e)
            {
                Logger.LogDebug(null, e, "Attempted to close an already closed socket.");
            }

            Socket.Dispose();
        }
    }
}
