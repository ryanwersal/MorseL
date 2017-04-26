using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MorseL.Common;
using MorseL.Common.Serialization;
using MorseL.Sockets;

namespace MorseL
{
    public static class ChannelExtensions
    {
        public static async Task InvokeClientMethodAsync(this IChannel channel, string methodName, object[] args)
        {
            // TODO: Serializer settings?
            var message = new Message()
            {
                MessageType = MessageType.ClientMethodInvocation,
                Data = Json.SerializeObject(new InvocationDescriptor()
                {
                    MethodName = methodName,
                    Arguments = args
                })
            };

            await channel.SendMessageAsync(message).ConfigureAwait(false);
        }
    }
}
