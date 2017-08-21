using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MorseL.Common;
using MorseL.Common.Serialization;
using MorseL.Scaleout;
using MorseL.Sockets;

namespace MorseL
{
    public class ClientsDispatcher
    {
        private readonly ILogger _logger;
        private WebSocketConnectionManager Manager { get; }
        private IBackplane Backplane { get; }

        public ClientsDispatcher(WebSocketConnectionManager manager, IBackplane backplane, ILogger<ClientsDispatcher> logger)
        {
            Manager = manager;
            Backplane = backplane;
            _logger = logger;

            All = new ClientInvoker(
                async (methodName, args) =>
                {
                    var message = new Message()
                    {
                        MessageType = MessageType.ClientMethodInvocation,
                        Data = Json.SerializeObject(new InvocationDescriptor()
                        {
                            MethodName = methodName,
                            Arguments = args
                        })
                    };
                    await Backplane.SendMessageAllAsync(message).ConfigureAwait(false);
                },
                async msg =>
                {
                    await Backplane.SendMessageAllAsync(msg).ConfigureAwait(false);
                },
                async group => {
                    await  Backplane.SubscribeAll(group).ConfigureAwait(false);
                },
                async group => {
                    await Backplane.UnsubscribeAll(group).ConfigureAwait(false);
                });
        }

        public IClientInvoker All { get; }

        public IClientInvoker Client(string connectionId)
        {
            var connection = Manager.GetConnectionById(connectionId);

            return new ClientInvoker(
                async (methodName, args) => {
                    if (connection != null) {
                        await connection.Channel.InvokeClientMethodAsync(methodName, args).ConfigureAwait(false);
                    } else {
                        var message = new Message()
                        {
                            MessageType = MessageType.ClientMethodInvocation,
                            Data = Json.SerializeObject(new InvocationDescriptor()
                            {
                                MethodName = methodName,
                                Arguments = args
                            })
                        };
                        await Backplane.SendMessageAsync(connectionId, message).ConfigureAwait(false);
                    }
                },
                async msg => {
                    if (connection != null) {
                        await connection.Channel.SendMessageAsync(msg).ConfigureAwait(false);
                    } else {
                        await Backplane.SendMessageAsync(connectionId, msg).ConfigureAwait(false);
                    }
                },
                async group => {
                    await  Backplane.Subscribe(group, connectionId).ConfigureAwait(false);
                },
                async group => {
                    await Backplane.Unsubscribe(group, connectionId).ConfigureAwait(false);
                });
        }
    }
}
