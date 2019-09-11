using System.IO;
using MorseL.Client.WebSockets;

namespace MorseL.Client.Middleware
{
    public class ConnectionContext
    {
        public Stream Stream { get; set; }
        public WebSocketMessageType MessageType { get; set; }

        public ConnectionContext(WebSocketMessageType messageType, Stream data)
        {
            MessageType = messageType;
            Stream = data;
        }
    }
}
