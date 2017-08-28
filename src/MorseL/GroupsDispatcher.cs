using System;
using System.Threading.Tasks;
using MorseL.Common;
using MorseL.Common.Serialization;
using MorseL.Scaleout;

namespace MorseL
{
    public class GroupsDispatcher
    {
        private readonly  IBackplane _backplane;
        public GroupsDispatcher(IBackplane backplane)
        {
            _backplane = backplane;
        }

        public IClientInvoker Group(string group)
        {
            return new ClientInvoker(
                async (methodName, args) => {
                    var message = new Message()
                    {
                        MessageType = MessageType.ClientMethodInvocation,
                        Data = Json.SerializeObject(new InvocationDescriptor()
                        {
                            MethodName = methodName,
                            Arguments = args
                        })
                    };
                    await _backplane.SendMessageGroupAsync(group, message).ConfigureAwait(false);
                },
                async msg => {
                    await _backplane.SendMessageAsync(group, msg).ConfigureAwait(false);
                },
                (g) => {
                    throw new NotImplementedException();
                },
                (g) => {
                    throw new NotImplementedException();
                });
        }
    }
}
