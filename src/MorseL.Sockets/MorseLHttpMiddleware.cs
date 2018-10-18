using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MorseL.Diagnostics;
using MorseL.Sockets.Middleware;
using Nito.AsyncEx.Synchronous;
using MorseL.Common;

namespace MorseL.Sockets
{
    /// <summary>
    /// <para>Flow of Request</para>
    /// <para>
    /// HTTP Request comes into server (kestrel) and gets passed through HTTP middleware
    /// added to <see cref="Invoke(HttpContext)"/>. The request gets checked for a valid
    /// websocket and ignored by <see cref="MorseLHttpMiddleware"/> if one is not present.
    /// </para>
    /// <para>
    /// Afterwards, a <see cref="WebSocketHandler"/> is created to manage the life of the
    /// websocket. The websocket is accepted and passed to
    /// <see cref="WebSocketHandler.OnConnected(WebSocket, HttpContext)"/> to initiate
    /// the websockets connection. The receive loop is then started on the websocket,
    /// looping and calling
    /// <see cref="WebSocket.ReceiveAsync(ArraySegment{byte}, CancellationToken)"/> to grab
    /// the next data packet.
    /// </para>
    /// <para>
    /// Each received data packet is checked for a text message or a close message. In the
    /// event of a close packet,
    /// <see cref="WebSocketHandler.OnDisconnected(WebSocket, Exception)"/> is called,
    /// otherwise <see cref="WebSocketHandler.ReceiveAsync(Connection, string)"/> is passed
    /// the data packet to handle the incoming packet.
    /// </para>
    /// <para>Exceptions</para>
    /// <para>
    /// Each inbound request marks its own long-running execution flow. The request lasts
    /// for as long as the web socket is connected and thus exceptions can be thrown and
    /// observed through out the entire process.
    /// </para>
    /// </summary>
    public class MorseLHttpMiddleware
    {
        private readonly ILogger _logger;
        private readonly RequestDelegate _next;

        private Type HandlerType { get; }

        public MorseLHttpMiddleware(
            ILogger<MorseLHttpMiddleware> logger, RequestDelegate next, Type handlerType)
        {
            _logger = logger;
            _next = next;
            HandlerType = handlerType;
        }

        public async Task Invoke(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                await _next(context);
                return;
            }

            var cts = new CancellationTokenSource();
            Exception pendingException = null;

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

                    await Receive(socket, connection, middleware, cts,
                        async (result, serializedInvocationDescriptor) =>
                        {
                            if (result.MessageType == WebSocketMessageType.Text || result.MessageType == WebSocketMessageType.Binary)
                            {
                                // We call the connection arg'd method directly to allow for middleware to override the IChannel
                                var unawaitedTask = Task.Run(async () =>
                                    {
                                        try
                                        {
                                            await handler.ReceiveAsync(connection, serializedInvocationDescriptor).ConfigureAwait(false);
                                        }
                                        catch (Exception exception)
                                        {
                                            // An exception here means it was thrown during message handling (not in the Hub method itself)
                                            // This is a big issue (likely message serialization) and can't be "saved"
                                            // We'll store the exception and rethrow after the receive loop is brought down
                                            pendingException = exception;
                                            cts.Cancel();
                                        }
                                    }).ConfigureAwait(false);
                            }
                            else if (result.MessageType == WebSocketMessageType.Close)
                            {
                                await handler.OnDisconnected(socket, null);
                            }
                        }).ConfigureAwait(false);

                    // Rethrow any exception thrown during message processing that broke our receive loop
                    if (pendingException != null)
                    {
                        ExceptionDispatchInfo.Capture(pendingException).Throw();
                    }
                }
                catch (Exception exception)
                {
                    if (pendingException == null || pendingException != exception)
                    {
                        pendingException = exception;
                    }

                    _logger?.LogError(new EventId(), exception, "Exception thrown during receive loop");

                    if (exception is MorseLException)
                    {
                        // For now, rethrow morsel exceptions
                        throw;
                    }
                }
                finally
                {
                    try
                    {
                        await handler.OnDisconnected(socket, pendingException);
                    }
                    catch (WebSocketException webSocketException)
                    {
                        _logger?.LogWarning(new EventId(), webSocketException, "Exception attempting to close socket");
                    }

                    cts.Dispose();
                }
            }
        }

        private async Task Receive(
            WebSocket socket, Connection connection, List<IMiddleware> middleware,
            CancellationTokenSource cts, Func<WebSocketReceiveResult, string, Task> handleMessage)
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
                        // WTF is going on?
                        // .net core doesn't seem to care about a passed in cancellation
                        // token, that's what :(
                        var task = socket.ReceiveAsync(buffer, cts.Token);

                        while (!task.IsCompleted && !cts.IsCancellationRequested)
                        {
                            await Task.Delay(100).ConfigureAwait(false);
                        }

                        if (cts.IsCancellationRequested)
                        {
                            // Stop. Immediately
                            return;
                        }

                        // Grab or throw based on the actual task result
                        result = await task;

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
                            using (_logger?.Tracer($"Middleware[{iterator.Current.GetType()}].ReceiveAsync(...)"))
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

                            using (_logger?.Tracer("Receive.handleMessage(...)"))
                            {
                                await handleMessage(result, serializedInvocationDescriptor);
                            }
                        }
                    };

                    await delegator
                        .Invoke(context)
                        .ContinueWith(task => {
                            iterator.Dispose();

                            task.WaitAndUnwrapException();
                        })
                        .ConfigureAwait(false);
                }
            }
        }
    }
}
