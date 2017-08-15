using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MorseL.Common;
using MorseL.Sockets.Middleware;

namespace MorseL.Sockets
{
    /// <summary>
    /// Abstraction around communication streams.
    /// </summary>
    public interface IChannel
    {
        ChannelState State { get; }
        Task SendAsync(Stream stream);
        Task DisposeAsync();
    }

    public enum ChannelState
    {
        None,
        Connecting,
        Open,
        CloseSent,
        CloseReceived,
        Closed,
        Aborted
    }
}
