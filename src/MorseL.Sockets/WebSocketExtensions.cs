using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MorseL.Common;
using MorseL.Common.Serialization;

namespace MorseL.Sockets
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
    }
}
