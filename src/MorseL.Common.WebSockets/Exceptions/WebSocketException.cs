using System;
using System.Collections.Generic;
using System.Text;

namespace MorseL.Common.WebSockets.Exceptions
{
    public class MorseLWebSocketException : MorseLException
    {
        public MorseLWebSocketException(string message) : base(message) { }

        public MorseLWebSocketException(string message, Exception innerException) : base(message, innerException) { }
    }
}
