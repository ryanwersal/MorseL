using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MorseL.Shared.Tests
{
    public class PortPool
    {
        public readonly static PortPool Instance = new PortPool(5000, 6000);

        private readonly ConcurrentBag<int> _availablePorts = new ConcurrentBag<int>();

        public TimeSpan ReleasedPortIdleTime { get; private set; }

        public (int Start, int End) Range { get; private set; }

        public PortPool(int start, int end, int releasedPortIdleTimeInMilliseconds = 5000)
            : this(start, end, TimeSpan.FromMilliseconds(releasedPortIdleTimeInMilliseconds)) { }

        public PortPool(int start, int end, TimeSpan releasedPortIdleTime)
        {
            Range = (start, end);
            ReleasedPortIdleTime = releasedPortIdleTime;

            // Add all the available ports
            for (var i = start; i < end; i++)
            {
                _availablePorts.Add(i);
            }
        }

        public Task<PortInstance> NextAsync()
        {
            var tcs = new TaskCompletionSource<int>();

            return Task.Run(async () =>
            {
                int port;

                while (!_availablePorts.TryTake(out port))
                {
                    await Task.Delay(100);
                }

                return new PortInstance(this, port);
            });
        }

        public void Return(int port)
        {
            Task.Run(async () =>
            {
                // Wait a little while before we add the port back in
                await Task.Delay(ReleasedPortIdleTime);
                _availablePorts.Add(port);
            });
        }
    }

    public class PortInstance : IDisposable
    {
        private readonly PortPool _pool;
        public int Port { get; private set; }

        public PortInstance(PortPool pool, int port)
        {
            _pool = pool;
            Port = port;
        }

        public void Dispose()
        {
            _pool.Return(Port);
        }
    }
}
