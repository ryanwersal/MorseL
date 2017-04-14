using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace WebSocketManager.Client
{
    public class ReceivedMessage : IDisposable
    {
        public WebSocketMessageType MessageType { get; }
        public MemoryStream MemoryStream { get; }

        public ReceivedMessage(WebSocketMessageType messageType, MemoryStream stream)
        {
            MessageType = messageType;
            MemoryStream = stream;
        }

        public Task<byte[]> ToBytesAsync()
        {
            // TODO: Test.
            return Task.FromResult(MemoryStream.ToArray());
        }

        public async Task<string> ToStringAsync()
        {
            using (var reader = new StreamReader(MemoryStream, Encoding.UTF8))
            {
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            MemoryStream?.Dispose();
        }
    }
}
