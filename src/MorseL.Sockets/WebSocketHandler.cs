using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MorseL.Common;
using MorseL.Sockets.Middleware;

namespace MorseL.Sockets
{
    public abstract class WebSocketHandler
    {
        private readonly IServiceProvider _services;
        private readonly ILogger _logger;
        protected WebSocketConnectionManager WebSocketConnectionManager { get; }

        protected WebSocketHandler(IServiceProvider services, ILoggerFactory loggerFactory)
        {
            _services = services;
            WebSocketConnectionManager = services.GetRequiredService<WebSocketConnectionManager>();
            _logger = loggerFactory.CreateLogger<WebSocketHandler>();
        }

        public async Task<Connection> OnConnected(WebSocket socket, HttpContext context)
        {
            // Create the websocket channel / connection
            var channel = ActivatorUtilities.CreateInstance<WebSocketChannel>(_services, socket);
            var connection = WebSocketConnectionManager.AddConnection(channel);

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
                await WebSocketConnectionManager.RemoveConnection(connection.Id);
                throw;
            }

            return connection;
        }

        public async Task OnDisconnected(WebSocket socket, Exception exception)
        {
            var connection = WebSocketConnectionManager.GetConnection(socket);

            try
            {
                await WebSocketConnectionManager.RemoveConnection(connection.Id).ConfigureAwait(false);
            }
            catch (Exception removeException)
            {
                // Likely the same reason why we disconnected
                if (!removeException.Equals(exception))
                {
                    _logger.LogError(removeException.Message);
                }
            }

            _logger.LogInformation($"Connection closed for ID {connection.Id}");

            await OnDisconnectedAsync(connection, exception);
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