using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MorseL.Sockets.Middleware
{
    /// <summary>
    /// Middleware that is invoked during websocket communication giving consumers the
    /// ability to transform the data before it is used by MorseL.
    /// </summary>
    public interface IMiddleware
    {
        /// <summary>
        /// Called when the connection is sending data.
        /// </summary>
        /// <param name="context">The communication context</param>
        /// <param name="next">The next middleware in the chain</param>
        /// <returns></returns>
        Task SendAsync(ConnectionContext context, MiddlewareDelegate next);
        /// <summary>
        /// Called when the connection is receiving data.
        /// </summary>
        /// <param name="context">The communication context</param>
        /// <param name="next">The next middleware in the chain</param>
        /// <returns></returns>
        Task ReceiveAsync(ConnectionContext context, MiddlewareDelegate next);
    }
}
