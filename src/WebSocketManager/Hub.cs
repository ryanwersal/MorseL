using System;
using System.Collections.Generic;
using System.Text;

namespace WebSocketManager
{
    public abstract class Hub : WebSocketHandler
    {
        protected Hub(WebSocketConnectionManager webSocketConnectionManager) : base(webSocketConnectionManager)
        {
            Clients = new ClientsDispatcher(webSocketConnectionManager);
        }

        public ClientsDispatcher Clients { get; }
    }
}
