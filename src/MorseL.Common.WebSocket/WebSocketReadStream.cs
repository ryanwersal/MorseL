using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MorseL.Common.WebSockets.Exceptions;

namespace MorseL.Common.WebSockets
{
    public class WebSocketReadStream : Stream
    {
        public WebSocketReceiveResult Result { get; private set; }
        private WebSocket _webSocket;
        private bool _isFinalFrameRead = false;
        private long _position = 0;

        private volatile bool _hasPeekByte;
        private volatile byte _peekByte;

        public WebSocketReadStream(WebSocket webSocket)
        {
            _webSocket = webSocket;
        }

        public override bool CanRead => _webSocket.State != WebSocketState.Closed
            && _webSocket.State != WebSocketState.CloseReceived
            && _webSocket.State != WebSocketState.CloseSent
            && _webSocket.State != WebSocketState.Aborted;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => _position; set => throw new NotImplementedException(); }

        public override void Flush() { }

        public async Task WaitForDataAsync(CancellationToken ct = default(CancellationToken))
        {
            // If we have a peek byte already then we have data!
            if (_hasPeekByte) return;

            var buffer = new byte[1];

            // We save the old position as we don't want the position affected
            // by our peeking
            var oldPosition = _position;

            // Read 1 byte
            var bytesRead = await ReadAsync(buffer, 0, 1, ct);

            // Reset the position
            _position = oldPosition;

            if (bytesRead == 1)
            {
                // If we read a byte, save it in the peek byte
                _hasPeekByte = true;
                _peekByte = buffer[0];
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // If we're not reading anything don't read anything
            if (count == 0) return 0;

            if (_isFinalFrameRead)
            {
                if (_hasPeekByte)
                {
                    // If we read the last frame and we have a peek byte then that
                    // means the peek byte was the last frame, so we return that and
                    // clear the peek byte. This will ensure all subsequent reads will
                    // return 0 as we have concluded the read
                    buffer[offset] = _peekByte;
                    _hasPeekByte = false;
                    return 1;
                }

                // No more data, return 0
                return 0;
            }

            int bytesRead = 0;

            if (_hasPeekByte)
            {
                // If we're reading and we have a peek byte then we need to first
                // prepend our read data with the peek byte.
                buffer[offset] = _peekByte;
                _hasPeekByte = false;

                // Increment the offset to step over the peek byte
                offset++;
                // Decrement the amount of bytes we need to read
                count--;

                // Increment our position to account for the peek byte
                _position++;

                // Increment the number of bytes read
                bytesRead++;

                // If we don't need to read anymore then we're only returning
                // the peek byte
                if (count == 0) return 1;
            }

            try
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, count), cancellationToken);
                _isFinalFrameRead = result.EndOfMessage;
                Result = result;

                _position += result.Count;
                bytesRead += result.Count;

                if (bytesRead == 0 && result.MessageType == WebSocketMessageType.Close)
                {
                     throw new WebSocketClosedException(result.CloseStatusDescription, result.CloseStatus);
                }

                return bytesRead;
            }
            // WebSocket.ReceiveAsync will throw a TaskCanceledException if the sending socket
            // closes the connection while we're waiting.
            catch (TaskCanceledException e)
            {
                if (_webSocket.State == WebSocketState.Aborted)
                {
                    throw new WebSocketClosedException("Sending socket likely closed the connection", e);
                }

                throw new WebSocketClosedException(e.Message);
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
