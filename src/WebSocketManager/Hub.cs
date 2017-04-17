using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace WebSocketManager
{
    public abstract class Hub : WebSocketHandler
    {
        protected Hub(WebSocketConnectionManager webSocketConnectionManager) : base(webSocketConnectionManager)
        {
            Clients = new ClientsDispatcher(webSocketConnectionManager);
            Groups = new GroupsManager();
        }

        public override async Task OnConnected(Connection connection)
        {
            await base.OnConnected(connection);

            Context = new CallerContext(connection);
        }

        public override async Task OnDisconnected(Connection connection)
        {
            await base.OnDisconnected(connection);

            Context = null;
        }

        public ClientsDispatcher Clients { get; }

        public CallerContext Context { get; set; }

        public GroupsManager Groups { get; }

        public virtual Task OnConnectedAsync()
        {
            return Task.CompletedTask;
        }

        public virtual Task OnDisconnectedAsync(Exception exception)
        {
            return Task.CompletedTask;
        }
    }
}
