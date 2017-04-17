using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WebSocketManager.Common;

namespace WebSocketManager.Sockets
{
    public interface IChannel
    {
        Task SendMessageAsync(Message message);
    }
}
