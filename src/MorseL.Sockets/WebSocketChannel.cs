using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using MorseL.Sockets.Middleware;

namespace MorseL.Sockets
{
    public class WebSocketChannel : IChannel
    {
        internal readonly WebSocket Socket;
        internal Connection Connection;
        internal IEnumerable<IMiddleware> Middleware;
        private SemaphoreSlim _writeLock = new SemaphoreSlim(1);

        public WebSocketChannel(WebSocket socket, IEnumerable<IMiddleware> middleware)
        {
            Socket = socket;
            Middleware = middleware ?? new List<IMiddleware>();
        }

        public ChannelState State => Socket?.State != null ? (ChannelState) Socket.State : ChannelState.None;

        public async Task SendAsync(Stream stream)
        {
            var context = new ConnectionContext(Connection, stream);
            var iterator = Middleware.GetEnumerator();
            MiddlewareDelegate delegator = null;
            delegator = async transformedContext =>
            {
                if (iterator.MoveNext())
                {
                    await iterator.Current.SendAsync(context, delegator).ConfigureAwait(false);
                }
                else
                {
                    await InternalSendAsync(transformedContext.Stream).ConfigureAwait(false);
                }
            };

            await delegator
                .Invoke(context)
                .ContinueWith(task => iterator.Dispose())
                .ConfigureAwait(false);
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
            await Socket?.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            Socket.Dispose();
        }
    }
}
