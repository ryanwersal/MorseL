using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WebSocketManager.Sockets.Internal;

namespace WebSocketManager.Sockets
{
    public class WebSocketManagerMiddleware
    {
        private readonly RequestDelegate _next;
        private Type HandlerType { get; }

        public WebSocketManagerMiddleware(RequestDelegate next, Type handlerType)
        {
            _next = next;
            HandlerType = handlerType;
        }

        public async Task Invoke(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest) return;

            var handler = ActivatorUtilities.CreateInstance(context.RequestServices, HandlerType) as WebSocketHandler;
            if (handler == null)
            {
                throw new Exception("Invalid handler type specified. Must be a Hub.");
            }

            var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);

            await handler.OnConnected(socket, context).ConfigureAwait(false);

            try
            {
                // Jump onto the thread pool thread so blocking user code doesn't block the setup of the
                // connection and transport
                await AwaitableThreadPool.Yield();

                await Receive(socket, async (result, serializedInvocationDescriptor) =>
                {
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        await handler.ReceiveAsync(socket, result, serializedInvocationDescriptor).ConfigureAwait(false);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await handler.OnDisconnected(socket, null);
                    }
                });
            }
            catch (WebSocketException)
            {
                throw;
            }
            catch (Exception exception)
            {
                await handler.OnDisconnected(socket, exception);
            }

            //TODO - investigate the Kestrel exception thrown when this is the last middleware
            //await _next.Invoke(context);
        }

        private async Task Receive(WebSocket socket, Action<WebSocketReceiveResult, string> handleMessage)
        {
            while (socket.State == WebSocketState.Open)
            {
                ArraySegment<Byte> buffer = new ArraySegment<byte>(new Byte[1024 * 4]);
                string serializedInvocationDescriptor = null;
                WebSocketReceiveResult result = null;
                using (var ms = new MemoryStream())
                {
                    do
                    {
                        result = await socket.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
                        ms.Write(buffer.Array, buffer.Offset, result.Count);
                    }
                    while (!result.EndOfMessage);

                    ms.Seek(0, SeekOrigin.Begin);

                    using (var reader = new StreamReader(ms, Encoding.UTF8))
                    {
                        serializedInvocationDescriptor = await reader.ReadToEndAsync().ConfigureAwait(false);
                    }
                }

                handleMessage(result, serializedInvocationDescriptor);
            }
        }
    }
}
