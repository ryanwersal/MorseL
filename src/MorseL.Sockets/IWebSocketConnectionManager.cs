using System;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace MorseL.Sockets
{
    public interface IWebSocketConnectionManager
    {
        Connection GetConnection(WebSocket socket);
        Connection GetConnectionById(string connectionId);

        Connection AddConnection(IChannel channel);
        Task RemoveConnection(string id);
    }
}
