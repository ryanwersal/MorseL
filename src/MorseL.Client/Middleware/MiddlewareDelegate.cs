using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MorseL.Client.Middleware
{
    public delegate Task RecieveDelegate(ConnectionContext packet);
    public delegate Task TransmitDelegate(Stream stream);
}
