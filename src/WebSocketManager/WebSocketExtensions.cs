using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSocketManager.Common;
using WebSocketManager.Common.Serialization;

namespace WebSocketManager
{
    public static class WebSocketExtensions
    {
        public static async Task SendMessageAsync(this WebSocket socket, Message message)
        {
            if (socket.State != WebSocketState.Open)
                return;

            // TODO: Serializer settings? Usage is inconsistent in the entire solution.
            var serializedMessage = Json.SerializeObject(message);
            var bytes = Encoding.ASCII.GetBytes(serializedMessage);
            var data = new ArraySegment<byte>(bytes, 0, serializedMessage.Length);
            await socket.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
        }

        public static async Task InvokeClientMethodAsync(this WebSocket socket, string methodName, object[] args)
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

            await socket.SendMessageAsync(message).ConfigureAwait(false);
        }
    }
}
