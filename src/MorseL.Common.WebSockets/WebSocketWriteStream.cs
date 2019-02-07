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
    public class WebSocketWriteStream : Stream
    {
        public WebSocketReceiveResult Result { get; private set; }
        private WebSocket _webSocket;
        private bool _hasWrittenFinalFrame = false;
        private bool _hasWrittenFirstFrame = false;
        private bool _throwOnOverDispose = true;

        public WebSocketWriteStream(WebSocket webSocket, bool throwOnOverDispose = true)
        {
            _webSocket = webSocket;
            _throwOnOverDispose = throwOnOverDispose;
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => _webSocket.State != WebSocketState.Closed
            && _webSocket.State != WebSocketState.CloseReceived
            && _webSocket.State != WebSocketState.CloseSent
            && _webSocket.State != WebSocketState.Aborted;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
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
            if (_hasWrittenFinalFrame)
            {
                throw new WebSocketStreamClosedException("The WebSocketWriteStream has been closed");
            }

            try
            {
                _hasWrittenFirstFrame = _hasWrittenFirstFrame || count > 0;
                if (count > 0)
                {
                    _webSocket.SendAsync(new ArraySegment<byte>(buffer, offset, count), WebSocketMessageType.Text, false, CancellationToken.None).Wait();
                }
            }
            // WebSocket.WriteAsync will throw a TaskCanceledException if the sending socket
            // closes the connection while we're waiting.
            catch (TaskCanceledException e)
            {
                if (_webSocket.State == WebSocketState.Aborted)
                {
                    throw new WebSocketClosedException("Receiving socket likely closed the connection", e);
                }

                throw new WebSocketClosedException(e.Message);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (_hasWrittenFinalFrame)
            {
                if (!_throwOnOverDispose) return;

                throw new WebSocketStreamClosedException("The WebSocketWriteStream has been closed");
            }

            if (_hasWrittenFirstFrame)
            {
                // We only _actually_ need to send the final frame
                // if we send data
                _webSocket.SendAsync(new ArraySegment<byte>(Array.Empty<byte>()), WebSocketMessageType.Text, true, CancellationToken.None).Wait();
            }

            _hasWrittenFinalFrame = true;
        }
    }
}
