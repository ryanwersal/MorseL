using System.Threading.Tasks;
using WebSocketManager;
using WebSocketManager.Common;
using WebSocketManager.Sockets;

namespace ChatApplication.Hubs
{
    public class ChatHub : Hub
    {
        public override async Task OnConnectedAsync(Connection connection)
        {
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

        public async Task OnDisconnectedAsync(Connection connection)
        {
            var message = new Message()
            {
                MessageType = MessageType.Text,
                Data = $"{connection.Id} disconnected"
            };

            await Clients.All.SendMessageAsync(message);
        }
    }
}
