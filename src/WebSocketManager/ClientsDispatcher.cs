using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WebSocketManager.Common;

namespace WebSocketManager
{
    public class ClientInvoker : IClientInvoker
    {
        private Func<string, object[],  Task> InvokeFunc { get; }
        private Func<Message, Task> SendMessageFunc { get; }

        public ClientInvoker(Func<string, object[], Task> invokeFunc, Func<Message, Task> sendMessageFunc)
        {
            InvokeFunc = invokeFunc;
            SendMessageFunc = sendMessageFunc;
        }

        public Task InvokeAsync(string methodName, params object[] args)
        {
            return InvokeFunc.Invoke(methodName, args);
        }

        public Task SendMessageAsync(Message message)
        {
            return SendMessageFunc.Invoke(message);
        }
    }

    public class ClientsDispatcher
    {
        private WebSocketConnectionManager Manager { get; }

        public ClientsDispatcher(WebSocketConnectionManager manager)
        {
            Manager = manager;

            All = new ClientInvoker(
                async (methodName, args) =>
                {
                    foreach (var socket in Manager.GetAll())
                    {
                        await socket.InvokeClientMethodAsync(methodName, args).ConfigureAwait(false);
                    }
                },
                async msg =>
                {
                    foreach (var socket in Manager.GetAll())
                    {
                        await socket.SendMessageAsync(msg).ConfigureAwait(false);
                    }
                });
        }

        public IClientInvoker All { get; }

        public IClientInvoker Client(string connectionId)
        {
            var socket = Manager.GetSocketById(connectionId);
            return new ClientInvoker(
                async (methodName, args) => await socket.InvokeClientMethodAsync(methodName, args).ConfigureAwait(false),
                async msg => await socket.SendMessageAsync(msg).ConfigureAwait(false));
        }
    }
}
