using System.Net.WebSockets;
using System.Threading.Tasks;
using WebSocketManager;
using WebSocketManager.Common;

namespace ChatApplication
{
    public class ChatHandler : Hub
    {
        public ChatHandler(WebSocketConnectionManager webSocketConnectionManager) : base(webSocketConnectionManager)
        {}

        public override async Task OnConnected(Connection connection)
        {
            await base.OnConnected(connection);

            var message = new Message()
            {
                MessageType = MessageType.Text,
                Data = $"{connection.Id} is now connected"
            };

            await Clients.All.SendMessageAsync(message);
        }

        public async Task SendMessage(string socketId, string message)
        {
            await Clients.All.InvokeAsync("receiveMessage", socketId, message);
        }

        public string Ping()
        {
            return "Pong";
        }

        public override async Task OnDisconnected(Connection connection)
        {
            await base.OnDisconnected(connection);

            var message = new Message()
            {
                MessageType = MessageType.Text,
                Data = $"{connection.Id} disconnected"
            };

            await Clients.All.SendMessageAsync(message);
        }
    }
}
