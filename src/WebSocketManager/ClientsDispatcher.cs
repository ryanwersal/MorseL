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
                    foreach (var connection in Manager.GetAll())
                    {
                        await connection.Socket.InvokeClientMethodAsync(methodName, args).ConfigureAwait(false);
                    }
                },
                async msg =>
                {
                    foreach (var connection in Manager.GetAll())
                    {
                        await connection.Socket.SendMessageAsync(msg).ConfigureAwait(false);
                    }
                });
        }

        public IClientInvoker All { get; }

        public IClientInvoker Group(string groupId)
        {
            return new ClientInvoker(
                (methodName, args) => Task.CompletedTask,
                msg => Task.CompletedTask);
        }

        public IClientInvoker Client(string connectionId)
        {
            var connection = Manager.GetConnectionById(connectionId);
            return new ClientInvoker(
                async (methodName, args) => await connection.Socket.InvokeClientMethodAsync(methodName, args).ConfigureAwait(false),
                async msg => await connection.Socket.SendMessageAsync(msg).ConfigureAwait(false));
        }
    }
}
