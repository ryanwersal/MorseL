using System.Threading.Tasks;
using MorseL;
using MorseL.Common;
using MorseL.Sockets;

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

        public async Task<string> Ping()
        {
            await Clients.Client(Context.ConnectionId).InvokeAsync("adfasdf");
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
