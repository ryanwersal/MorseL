using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;

namespace MorseL.Common.WebSockets.Exceptions
{
    public class WebSocketClosedException : Exception
    {
        public WebSocketCloseStatus? CloseStatus { get; private set; }

        public WebSocketClosedException(string message, WebSocketCloseStatus? closeStatus = null) : base(message)
        {
            CloseStatus = closeStatus;
        }

        public WebSocketClosedException(string message, Exception innerException, WebSocketCloseStatus? closeStatus = null) : base(message, innerException)
        {
            CloseStatus = closeStatus;
        }
    }
}
