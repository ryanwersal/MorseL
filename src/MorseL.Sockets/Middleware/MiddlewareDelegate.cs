using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MorseL.Sockets.Middleware
{
    public delegate Task MiddlewareDelegate(ConnectionContext context);
}
