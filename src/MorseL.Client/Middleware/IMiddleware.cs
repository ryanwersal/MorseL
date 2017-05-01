using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using MorseL.Client.WebSockets;

namespace MorseL.Client.Middleware
{
    /// <summary>
    /// Middleware that is invoked during websocket communication giving consumers the
    /// ability to transform the data before it is used by MorseL.
    /// </summary>
    public interface IMiddleware
    {
        /// <summary>
        /// Called when the connection is transmitting data.
        /// </summary>
        /// <param name="data">The data being transmitted</param>
        /// <param name="next">The next middleware in the chain</param>
        Task SendAsync(string data , TransmitDelegate next);
        /// <summary>
        /// Called when the connection is receiving data.
        /// </summary>
        /// <param name="packet">The received data</param>
        /// <param name="next">The next middleware in the chain</param>
        Task RecieveAsync(WebSocketPacket packet, RecieveDelegate next);
    }
}
