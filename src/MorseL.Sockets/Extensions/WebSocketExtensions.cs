using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using MorseL.Common.WebSockets;

namespace MorseL.Sockets.Extensions
{
    public static class WebSocketExtensions
    {
        public static WebSocketReadStream GetReadStream(this WebSocket socket)
        {
            return new WebSocketReadStream(socket);
        }
    }
}
