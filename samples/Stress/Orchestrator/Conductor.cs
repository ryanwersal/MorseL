using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Orchestrator;
using Serilog;

namespace Conductor
{
    public class Conductor : IDisposable
    {
        private readonly int _clientCount;

        private Host _host;
        private readonly List<TestClient> _clients = new List<TestClient>();
        private bool _close = false;

        public Conductor(int clientCount)
        {
            _clientCount = clientCount;
        }

        public async Task StartAsync()
        {
            Log.Information("Starting MorseL host");
            _host = new Host();
            await _host.StartAsync();

            await Task.Delay(2000);

            Log.Information($"Starting MorseL {_clientCount} clients");
            for (var i = 0; i < _clientCount; i++)
            {
                var client = new TestClient($"Client {i+1}/{_clientCount}");
                _clients.Add(client);
                await client.StartAsync();
            }

            Log.Information("Running...");

            Console.CancelKeyPress += (sender, args) => _close = true;

            while (!_close) { }

            Dispose();
        }

        public void Dispose()
        {
            _host?.Dispose();
            foreach (var client in _clients)
            {
                client.Dispose();
            }
        }
    }
}
