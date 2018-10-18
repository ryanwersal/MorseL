using System;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MorseL.Common;

[assembly: InternalsVisibleTo("MorseL.Scaleout.Tests")]
[assembly: InternalsVisibleTo("MorseL.Scaleout.Redis.Tests")]
namespace MorseL.Sockets
{
    /// <summary>
    /// <para>
    /// Handles managing a websockets lifetime and providing an abstraction around
    /// connected, disconnected, and message received states.
    /// </para>
    /// <para>
    /// TODO : Abstract Handler from WebSocket so HubXxxHandlers don't need to know
    /// about underlying transportation medium.
    /// </para>
    /// </summary>
    public abstract class WebSocketHandler
    {
        private readonly IServiceProvider _services;
        private readonly ILogger _logger;
        protected IWebSocketConnectionManager webSocketManager { get; }

        protected WebSocketHandler(IServiceProvider services, ILoggerFactory loggerFactory)
        {
            _services = services;
            webSocketManager = services.GetRequiredService<IWebSocketConnectionManager>();
            _logger = loggerFactory.CreateLogger<WebSocketHandler>();
        }

        internal async Task<Connection> OnConnected(WebSocket socket, HttpContext context)
        {
            // Create the websocket channel / connection
            var channel = ActivatorUtilities.CreateInstance<WebSocketChannel>(_services, socket);
            var connection = webSocketManager.AddConnection(channel);

            // Set the internal channel's reference to it's containing connection
            channel.Connection = connection;

            connection.User = context.User;

            _logger.LogInformation($"Connection established for ID {connection.Id}");

            try
            {
                await connection.Channel.SendMessageAsync(new Message()
                {
                    MessageType = MessageType.ConnectionEvent,
                    Data = connection.Id
                }).ConfigureAwait(false);

                await OnConnectedAsync(connection);
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                await webSocketManager.RemoveConnection(connection.Id);
                throw;
            }

            return connection;
        }

        internal async Task OnDisconnected(WebSocket socket, Exception exception)
        {
            var connection = webSocketManager.GetConnection(socket);

            if (connection != null)
            {
                try
                {
                    await webSocketManager.RemoveConnection(connection.Id).ConfigureAwait(false);
                }
                catch (Exception removeException)
                {
                    // Likely the same reason why we disconnected
                    if (!removeException.Equals(exception))
                    {
                        _logger.LogError(removeException.Message);
                    }
                }

                _logger.LogInformation($"Connection closed for ID {connection?.Id ?? "Unknown"}");

                await OnDisconnectedAsync(connection, exception);
            }
        }

        public virtual async Task OnConnectedAsync(Connection connection)
        {
            await Task.CompletedTask;
        }

        public virtual async Task OnDisconnectedAsync(Connection connection, Exception exception)
        {
            await Task.CompletedTask;
        }

        public abstract Task ReceiveAsync(Connection connection, string serializedInvocationDescriptor);
    }
}
