using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using WebSocketManager.Common;

namespace WebSocketManager.Sockets
{
    public class WebSocketChannel : IChannel
    {
        internal readonly WebSocket Socket;

        public WebSocketChannel(WebSocket socket)
        {
            Socket = socket;
        }

        public Task SendMessageAsync(Message message)
        {
            return Socket.SendMessageAsync(message);
        }
    }
}
