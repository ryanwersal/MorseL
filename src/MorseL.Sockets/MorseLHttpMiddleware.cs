using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MorseL.Diagnostics;
using MorseL.Sockets.Internal;
using MorseL.Sockets.Middleware;

namespace MorseL.Sockets
{
    public class MorseLHttpMiddleware
    {
        private readonly ILogger _logger;
        private readonly RequestDelegate _next;
        private Type HandlerType { get; }

        public MorseLHttpMiddleware(ILogger<MorseLHttpMiddleware> logger, RequestDelegate next, Type handlerType)
        {
            _logger = logger;
            _next = next;
            HandlerType = handlerType;
        }

        public async Task Invoke(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest) return;

            using (var serviceScope = context.RequestServices.CreateScope())
            {
                var services = serviceScope.ServiceProvider;
                var middleware = services.GetServices<IMiddleware>().ToList();

                var handler = ActivatorUtilities.CreateInstance(services, HandlerType) as WebSocketHandler;
                if (handler == null)
                {
                    throw new Exception("Invalid handler type specified. Must be a Hub.");
                }

                var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);

                try
                {
                    // TODO : Consider/Decide on pulling web socket manager outside of handler
                    var connection = await handler.OnConnected(socket, context).ConfigureAwait(false);

                    await Receive(socket, connection, middleware,
                        async (result, serializedInvocationDescriptor) =>
                        {
                            if (result.MessageType == WebSocketMessageType.Text)
                            {
                                // We call the connection arg'd method directly to allow for middleware to override the IChannel
                                await handler.ReceiveAsync(connection, serializedInvocationDescriptor)
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
                    _logger.LogWarning(exception.Message);

                    try
                    {
                        await handler.OnDisconnected(socket, exception);
                    }
                    catch (WebSocketException webSocketException)
                    {
                        _logger.LogWarning(webSocketException.Message);
                    }
                }

                //TODO - investigate the Kestrel exception thrown when this is the last middleware
                //await _next.Invoke(context);
            }
        }

        private async Task Receive(WebSocket socket, Connection connection, List<IMiddleware> middleware, Action<WebSocketReceiveResult, string> handleMessage)
        {
            while (socket.State == WebSocketState.Open)
            {
                ArraySegment<Byte> buffer = new ArraySegment<byte>(new Byte[1024 * 4]);
                string serializedInvocationDescriptor = null;
                WebSocketReceiveResult result = null;

                using (var ms = new MemoryStream())
                {
                    // TODO: Consider doing the actual receiving in another task rather than reading all at once
                    do
                    {
                        result = await socket.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
                        ms.Write(buffer.Array, buffer.Offset, result.Count);
                    } while (!result.EndOfMessage);

                    ms.Seek(0, SeekOrigin.Begin);

                    var context = new ConnectionContext(connection, ms);
                    var iterator = middleware.GetEnumerator();
                    MiddlewareDelegate delegator = null;
                    delegator = async tranformedContext =>
                    {
                        if (iterator.MoveNext())
                        {
                            using (_logger.Tracer($"Middleware[{iterator.Current.GetType()}].ReceiveAsync(...)"))
                            {
                                await iterator.Current.ReceiveAsync(context, delegator).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            using (var reader = new StreamReader(tranformedContext.Stream, Encoding.UTF8))
                            {
                                serializedInvocationDescriptor = await reader.ReadToEndAsync().ConfigureAwait(false);
                            }

                            using (_logger.Tracer("Receive.handleMessage(...)"))
                            {
                                handleMessage(result, serializedInvocationDescriptor);
                            }
                        }
                    };

                    await delegator
                        .Invoke(context)
                        .ContinueWith(task => iterator.Dispose())
                        .ConfigureAwait(false);
                }
            }
        }
    }
}
