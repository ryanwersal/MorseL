using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MorseL.Client.WebSockets;

namespace MorseL.Client.Middleware
{
    public delegate Task RecieveDelegate(WebSocketPacket packet);
    public delegate Task TransmitDelegate(string data);
}
