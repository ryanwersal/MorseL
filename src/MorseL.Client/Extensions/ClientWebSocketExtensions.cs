using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MorseL.Common.WebSockets;

namespace MorseL.Common.WebSocket.Extensions
{
    internal static class ClientWebSocketExtensions
    {
        /// <summary>
        /// Assume a default web socket message size of 100mb. The actual spec maximum size
        /// is 2 GB. But we won't go that high.
        /// </summary>
        private const int DEFAULT_WEBSOCKET_MESSAGE_SIZE = 100 * 1024 * 1024;

        private static byte[] _buffer;
        private static byte[] Buffer
        {
            get
            {
                if (_buffer == null)
                {
                    _buffer = new byte[1024 * 4];
                }

                return _buffer;
            }
        }

        public static async Task SendStreamAsync(this ClientWebSocket webSocket, Stream stream, int messageSize = DEFAULT_WEBSOCKET_MESSAGE_SIZE, CancellationToken ct = default(CancellationToken))
        {
            using (var writeStream = webSocket.GetWriteStream())
            {
                await stream.CopyToAsync(writeStream, 81920, ct);
            }
        }

        public static WebSocketReadStream GetReadStream(this ClientWebSocket webSocket)
        {
            return new WebSocketReadStream(webSocket);
        }

        public static WebSocketWriteStream GetWriteStream(this ClientWebSocket webSocket)
        {
            return new WebSocketWriteStream(webSocket, throwOnOverDispose: false);
        }
    }
}
