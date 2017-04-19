using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebSocketManager.Sockets.Internal;

namespace WebSocketManager.Sockets
{
    public class WebSocketManagerMiddleware
    {
        private readonly ILogger _logger;
        private readonly RequestDelegate _next;
        private Type HandlerType { get; }

        public WebSocketManagerMiddleware(ILogger<WebSocketManagerMiddleware> logger, RequestDelegate next, Type handlerType)
        {
            _logger = logger;
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

            try
            {
                // TODO : Consider/Decide on pulling web socket manager outside of handler
                await handler.OnConnected(socket, context).ConfigureAwait(false);

                await Receive(socket, async (result, serializedInvocationDescriptor) =>
                {
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        await handler.ReceiveAsync(socket, result, serializedInvocationDescriptor)
                            .ConfigureAwait(false);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await handler.OnDisconnected(socket, null);
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
                try
                {
                    await handler.OnDisconnected(socket, exception);
                }
                catch (WebSocketException webSocketException)
                {
                    _logger.LogError(webSocketException.Message);
                }
            }

            //TODO - investigate the Kestrel exception thrown when this is the last middleware
            await _next.Invoke(context);
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
