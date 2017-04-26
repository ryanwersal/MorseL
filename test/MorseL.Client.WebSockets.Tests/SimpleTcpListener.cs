using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MorseL.Client.WebSockets.Tests
{
    public class SimpleTcpListener : TcpListener, IDisposable
    {
        private bool _isListening = false;

        public SimpleTcpListener(IPAddress localaddr, int port) : base(localaddr, port)
        {
        }

        public SimpleTcpListener(IPEndPoint localEP) : base(localEP)
        {
        }

        public new void Start()
        {
            _isListening = true;
        }

        public new void Start(int backlog)
        {
            _isListening = true;
        }

        public void Dispose()
        {
            if (_isListening)
            {
                Stop();
            }
        }
    }
}
