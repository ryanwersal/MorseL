using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WebSocketManager.Common;

namespace WebSocketManager
{
    public static class WebSocketExtensions
    {
        public static async Task SendMessageAsync(this WebSocket socket, Message message)
        {
            if (socket.State != WebSocketState.Open)
                return;

            // TODO: Serializer settings? Usage is inconsistent in the entire solution.
            var serializedMessage = JsonConvert.SerializeObject(message);
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
                Data = JsonConvert.SerializeObject(new InvocationDescriptor()
                {
                    MethodName = methodName,
                    Arguments = args
                })
            };

            await socket.SendMessageAsync(message).ConfigureAwait(false);
        }
    }
}
