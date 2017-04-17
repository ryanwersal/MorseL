using System;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WebSocketManager.Common;
using WebSocketManager.Sockets;

namespace WebSocketManager
{
    public abstract class WebSocketHandler
    {
        protected WebSocketConnectionManager WebSocketConnectionManager { get; set; }

        protected WebSocketHandler(WebSocketConnectionManager webSocketConnectionManager)
        {
            WebSocketConnectionManager = webSocketConnectionManager;
        }

        public async Task OnConnected(WebSocket socket, HttpContext context)
        {
            var connection = WebSocketConnectionManager.AddSocket(socket);
            connection.User = context.User;

            await connection.Socket.SendMessageAsync(new Message()
            {
                MessageType = MessageType.ConnectionEvent,
                Data = connection.Id
            }).ConfigureAwait(false);

            await OnConnectedAsync(connection);
        }

        public async Task OnDisconnected(WebSocket socket, Exception exception)
        {
            var connection = WebSocketConnectionManager.GetConnection(socket);

            await WebSocketConnectionManager.RemoveConnection(connection.Id).ConfigureAwait(false);

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