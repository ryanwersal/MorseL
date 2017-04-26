using System;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MorseL.Common;

namespace MorseL.Sockets
{
    public abstract class WebSocketHandler
    {
        private readonly ILogger _logger;
        protected WebSocketConnectionManager WebSocketConnectionManager { get; }
        protected WebSocketHandler(IServiceProvider services, ILoggerFactory loggerFactory)
        {
            WebSocketConnectionManager = services.GetRequiredService<WebSocketConnectionManager>();
            _logger = loggerFactory.CreateLogger<WebSocketHandler>();
        }

        public async Task OnConnected(WebSocket socket, HttpContext context)
        {
            var connection = WebSocketConnectionManager.AddSocket(socket);
            connection.User = context.User;

            _logger.LogInformation($"Connection established for ID {connection.Id}");

            try
            {
                await connection.Socket.SendMessageAsync(new Message()
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

        public Task ReceiveAsync(WebSocket socket, WebSocketReceiveResult result, string serializedInvocationDescriptor)
        {
            return ReceiveAsync(WebSocketConnectionManager.GetConnection(socket), serializedInvocationDescriptor);
        }

        public abstract Task ReceiveAsync(Connection connection, string serializedInvocationDescriptor);
    }
}