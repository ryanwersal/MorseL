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
        private IBackplane _backplane { get; }

        public ClientsDispatcher(WebSocketConnectionManager manager, IBackplane backplane, ILogger<ClientsDispatcher> logger)
        {
            Manager = manager;
            _backplane = backplane;
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
                    await _backplane.SendMessageAllAsync(message).ConfigureAwait(false);
                },
                async msg =>
                {
                    await _backplane.SendMessageAllAsync(msg).ConfigureAwait(false);
                },
                async group => {
                    await  _backplane.SubscribeAll(group).ConfigureAwait(false);
                },
                async group => {
                    await _backplane.UnsubscribeAll(group).ConfigureAwait(false);
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
                        await _backplane.SendMessageAsync(connectionId, message).ConfigureAwait(false);
                    }
                },
                async msg => {
                    if (connection != null) {
                        await connection.Channel.SendMessageAsync(msg).ConfigureAwait(false);
                    } else {
                        await _backplane.SendMessageAsync(connectionId, msg).ConfigureAwait(false);
                    }
                },
                async group => {
                    await  _backplane.Subscribe(group, connectionId).ConfigureAwait(false);
                },
                async group => {
                    await _backplane.Unsubscribe(group, connectionId).ConfigureAwait(false);
                });
        }
    }
}
