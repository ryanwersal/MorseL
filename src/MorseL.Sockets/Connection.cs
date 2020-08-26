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

        private CancellationTokenSource _cancellationTokenSource { get; } = new CancellationTokenSource();
        public CancellationToken ConnectionCancellationToken => _cancellationTokenSource.Token;

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
            await Channel.DisposeAsync();
            _cancellationTokenSource.Cancel();
        }
    }
}
