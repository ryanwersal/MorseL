using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MorseL.Common;
using MorseL.Common.Serialization;

namespace MorseL.Scaleout
{
    public static class BackplaneExtensions
    {
        public static Task InvokeAsync(this IBackplane backplane, string connectionId, string methodName, params object[] arguments)
        {
            return backplane.SendMessageAsync(connectionId, new Message
            {
                MessageType = MessageType.ClientMethodInvocation,
                Data = Json.SerializeObject(new InvocationDescriptor()
                {
                    MethodName = methodName,
                    Arguments = arguments
                })
            });
        }

        public static Task InvokeGroupAsync(this IBackplane backplane, string groupName, string methodName, params object[] arguments)
        {
            return backplane.SendMessageGroupAsync(groupName, new Message
            {
                MessageType = MessageType.ClientMethodInvocation,
                Data = Json.SerializeObject(new InvocationDescriptor()
                {
                    MethodName = methodName,
                    Arguments = arguments
                })
            });
        }
    }
}
