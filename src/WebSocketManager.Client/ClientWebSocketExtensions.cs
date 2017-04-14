using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketManager.Client
{
    public static class ClientWebSocketExtensions
    {
        public static async Task<ReceivedMessage> ReceiveAllAsync(this ClientWebSocket socket, CancellationToken cancellationToken)
        {
            var buffer = new ArraySegment<byte>(new byte[4096]);
            var stream = new MemoryStream();

            WebSocketReceiveResult result = null;
            do
            {
                result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(buffer.Array, buffer.Offset, result.Count, cancellationToken).ConfigureAwait(false);
            } while (!result.EndOfMessage);

            stream.Seek(0, SeekOrigin.Begin);

            return new ReceivedMessage(result.MessageType, stream);
        }

        public static async Task SendAllAsync(this ClientWebSocket socket, string message,
            CancellationToken cancellationToken)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(message)))
            {
                int MAX_BUFFER_SIZE = 4096;
                var offset = 0;
                while (offset < stream.Length)
                {
                    var count = Math.Min((int)stream.Length - offset, MAX_BUFFER_SIZE);

                    using (var s = new MemoryStream())
                    {
                        await stream.CopyToAsync(s, count, cancellationToken).ConfigureAwait(false);
                        var buffer = s.ToArray();

                        var segment = new ArraySegment<byte>(buffer, offset, count);
                        var endOfMessage = count <= MAX_BUFFER_SIZE;

                        await socket.SendAsync(segment, WebSocketMessageType.Text, endOfMessage, cancellationToken)
                            .ConfigureAwait(false);
                        offset += segment.Count;
                    }
                }
            }
        }
    }
}
