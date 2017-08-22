using System.Threading.Tasks;
using MorseL;
using MorseL.Common;
using MorseL.Sockets;

namespace BackplanedHost.Hubs
{
    public class ChatHub : Hub
    {
        public override Task OnConnectedAsync(Connection connection)
        {
            var message = new Message()
            {
                MessageType = MessageType.Text,
                Data = $"{connection.Id} is now connected"
            };

            return Clients.All.SendMessageAsync(message);
        }

        public Task SendMessage(string socketId, string message)
        {
            return Clients.All.InvokeAsync("receiveMessage", socketId, message);
        }

        public Task SendTell(string socketId, string targetConnectionId, string message)
        {
            return Clients.Client(targetConnectionId).InvokeAsync("receiveMessage", socketId, message);
        }

        public Task SendGroup(string socketId, string targetGroup, string message)
        {
            return Groups.Group(targetGroup).InvokeAsync("receiveMessage", socketId, message);
        }

        public Task Subscribe(string group) {
            return Client.Subscribe(group);
        }

        public Task Unsubscribe(string group) {
            return Client.Unsubscribe(group);
        }

        public async Task<string> Ping()
        {
            await Clients.Client(Context.ConnectionId).InvokeAsync("adfasdf");
            return "Pong";
        }

        public Task OnDisconnectedAsync(Connection connection)
        {
            var message = new Message()
            {
                MessageType = MessageType.Text,
                Data = $"{connection.Id} disconnected"
            };

            return Clients.All.SendMessageAsync(message);
        }
    }
}