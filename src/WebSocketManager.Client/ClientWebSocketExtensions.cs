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
                var MAX_BUFFER_SIZE = 4096;
                var buffer = new byte[MAX_BUFFER_SIZE];

                using (var bufferedStream = new BufferedStream(stream, MAX_BUFFER_SIZE))
                {
                    var moreToSend = true;
                    do
                    {
                        await bufferedStream.ReadAsync(buffer, 0, MAX_BUFFER_SIZE, cancellationToken);
                        var segment = new ArraySegment<byte>(buffer, 0, MAX_BUFFER_SIZE);

                        moreToSend = bufferedStream.Position < bufferedStream.Length;

                        await socket.SendAsync(segment, WebSocketMessageType.Text,
                            !moreToSend, cancellationToken).ConfigureAwait(false);
                    } while (moreToSend);
                }
            }
        }
    }
}
