using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MorseL.Sockets.Middleware;

namespace MorseL.Sockets
{
    public class Connection : IDisposable
    {
        public string Id { get; }
        public ClaimsPrincipal User { get; set; }

        // TODO: Remove this and instead make connections transport-agnostic.
        public IChannel Channel { get; set; }

        public Connection(string id, IChannel channel)
        {
            Id = id;
            Channel = channel;
            User = new ClaimsPrincipal();
        }

        public void Dispose()
        {
            DisposeAsync().Wait();
        }

        public async Task DisposeAsync()
        {
            await ((WebSocketChannel)Channel).Socket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                    "Closed by manager.",
                    CancellationToken.None)
                .ConfigureAwait(false);
            ((WebSocketChannel) Channel).Socket.Dispose();
        }
    }
}
