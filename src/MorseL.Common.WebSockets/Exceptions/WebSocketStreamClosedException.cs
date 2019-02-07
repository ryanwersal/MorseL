using System;
using System.Collections.Generic;
using System.Text;

namespace MorseL.Common.WebSockets.Exceptions
{
    public class WebSocketStreamClosedException : MorseLWebSocketException
    {
        public WebSocketStreamClosedException(string message) : base(message) { }
    }
}
