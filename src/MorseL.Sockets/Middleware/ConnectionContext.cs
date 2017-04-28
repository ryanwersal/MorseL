using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MorseL.Sockets.Middleware
{
    /// <summary>
    /// A context object containing a reference to the associated Connection and
    /// the stream used for communication.
    /// </summary>
    public class ConnectionContext
    {
        /// <summary>
        /// The stream being used for communication.
        /// </summary>
        public Stream Stream { get; }
        /// <summary>
        /// The connection facilitating the communication.
        /// </summary>
        public Connection Connection { get; }

        public ConnectionContext(Connection connection, Stream stream)
        {
            Connection = connection;
            Stream = stream;
        }
    }
}
