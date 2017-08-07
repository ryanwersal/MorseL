using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MorseL.Sockets;

namespace MorseL
{
    public abstract class Hub : Hub<IClientInvoker>
    {
    }

    public abstract class Hub<TClient> : IDisposable
    {
        public virtual Task OnConnectedAsync(Connection connection)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnDisconnectedAsync(Exception exception)
        {
            return Task.CompletedTask;
        }

        public IClientInvoker Client { get; set; }

        public ClientsDispatcher Clients { get; set; }

        public HubCallerContext Context { get; set; }

        public GroupsDispatcher Groups { get; set; }

        public void Dispose()
        {
        }
    }
}
