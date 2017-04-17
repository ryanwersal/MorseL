using System;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketManager.Sockets
{
    public class Connection : IDisposable
    {
        public string Id { get; }
        public ClaimsPrincipal User { get; set; }

        // TODO: Remove this and instead make connections transport-agnostic.
        public IChannel Socket { get; }

        public Connection(string id, WebSocket socket)
        {
            Id = id;
            Socket = new WebSocketChannel(socket);
            User = new ClaimsPrincipal();
        }

        public void Dispose()
        {
            DisposeAsync().Wait();
        }

        public async Task DisposeAsync()
        {
            await ((WebSocketChannel)Socket).Socket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                    "Closed by manager.",
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
    }
}
