using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MorseL.Common;
using MorseL.Common.Serialization;

namespace MorseL.Shared.Tests
{
    public class LinkedFakeSocket : WebSocket
    {
        public override void Abort() { }

        public override void Dispose() { }

        public override WebSocketCloseStatus? CloseStatus => WebSocketCloseStatus.NormalClosure;
        public override string CloseStatusDescription => "";
        public override WebSocketState State => WebSocketState.Open;
        public override string SubProtocol => "";

        public bool CloseCalled { get; private set; } = false;

        private byte[] _buffer;
        private int _position = 0;

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage,
            CancellationToken cancellationToken)
        {
            var offset = 0;
            if (_buffer == null)
            {
                _buffer = new byte[buffer.Count];
            }
            else
            {
                var oldBuffer = _buffer;
                _buffer = new byte[_buffer.Length + buffer.Count];
                Array.Copy(oldBuffer, _buffer, oldBuffer.Length);
                offset = oldBuffer.Length;
            }

            Array.Copy(buffer.Array, buffer.Offset, _buffer, offset, buffer.Count);
            return Task.CompletedTask;
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            WebSocketReceiveResult result;

            var bytesRead = Math.Min(_buffer.Length - _position, buffer.Count);
            var bytesLeft = _buffer.Length - _position - bytesRead;
            if (bytesRead > 0)
            {
                result = new WebSocketReceiveResult(bytesRead, WebSocketMessageType.Text, bytesLeft <= 0);
            }
            else
            {
                result = new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
            }

            Array.Copy(_buffer, _position, buffer.Array, 0, bytesRead);
            _position += bytesRead;

            return Task.FromResult(result);
        }

        public async Task<string> ReadToEndAsync()
        {
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[1024 * 4]);

            using (var ms = new MemoryStream())
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
                    ms.Write(buffer.Array, buffer.Offset, result.Count);
                }
                while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);

                using (var reader = new StreamReader(ms, Encoding.UTF8))
                {
                    return await reader.ReadToEndAsync().ConfigureAwait(false);
                }
            }
        }


        public async Task<Message> ReadMessageAsync()
        {
            return Json.Deserialize<Message>(await ReadToEndAsync());
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            CloseCalled = true;
            return Task.CompletedTask;
        }
    }
}
