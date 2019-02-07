using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MorseL.Common.WebSockets;

namespace MorseL.Common.WebSocket.Extensions
{
    internal static class ClientWebSocketExtensions
    {
        public static WebSocketReadStream GetReadStream(this ClientWebSocket webSocket)
        {
            return new WebSocketReadStream(webSocket);
        }

        public static WebSocketWriteStream GetWriteStream(this ClientWebSocket webSocket)
        {
            return new WebSocketWriteStream(webSocket, throwOnOverDispose: false);
        }
    }
}
