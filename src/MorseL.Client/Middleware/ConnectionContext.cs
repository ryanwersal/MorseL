using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;

namespace MorseL.Client.Middleware
{
    public class ConnectionContext
    {
        public Stream Stream { get; set; }
        public ClientWebSocket ClientWebSocket { get; set; }

        public ConnectionContext(ClientWebSocket clientWebSocket, Stream stream)
        {
            ClientWebSocket = clientWebSocket;
            Stream = stream;
        }
    }
}
