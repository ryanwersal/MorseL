// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace MorseL.Client.WebSockets
{
    public class WebSocketPacket
    {
        public WebSocketMessageType MessageType { get; }
        public byte[] Data { get; }

        public WebSocketPacket(WebSocketMessageType messageType, byte[] data)
        {
            MessageType = messageType;
            Data = data;
        }
    }
}