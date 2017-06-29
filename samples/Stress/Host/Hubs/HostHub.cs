using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MorseL;
using MorseL.Common;
using MorseL.Sockets;
using Serilog;
using Serilog.Core;

namespace Host.Hubs
{
    public class HostHub : Hub
    {
        private ILogger _logger;

        private ILogger Logger => _logger.ForContext("ClientName", ConnectionName());

        private static readonly Dictionary<string, string> ConnectionNames = new Dictionary<string, string>();
        private static readonly Dictionary<string, Counter> Counts = new Dictionary<string, Counter>();

        public HostHub(ILogger logger)
        {
            _logger = logger;
        }

        public override Task OnConnectedAsync(Connection connection)
        {
            _logger.Information($"Connection established with {connection.Id}");
            return Task.CompletedTask;
        }

        public void Hello(string name)
        {
            ConnectionNames[Context.ConnectionId] = name;
        }

        public void Ping()
        {
            if (Count(nameof(Ping)).Increment() % 100 == 0)
            {
                Logger.Information("100 Ping's recieved");
            }
        }

        public async Task<string> ExpectResponse(string id, string input, int delay)
        {
            Logger.Information($"Fielding ExpectResponse request ({id}).");
            await Task.Delay(delay);
            Logger.Information($"Completed ExpectResponse request ({id}).");
            return input;
        }

        public Task OnDisconnectedAsync(Connection connection)
        {
            _logger.Information($"Connection terminated with {ConnectionName()}");
            return Task.CompletedTask;
        }

        private string ConnectionName()
        {
            return ConnectionNames.ContainsKey(Context.ConnectionId) ? ConnectionNames[Context.ConnectionId] : Context.ConnectionId;
        }

        private Counter Count(string target)
        {
            if (!Counts.ContainsKey(target))
            {
                Counts[target] = new Counter();
            }

            return Counts[target];
        }

        private class Counter
        {
            public int Value { get; set; } = 0;

            public int Increment()
            {
                return ++Value;
            }

            public int Decrement()
            {
                return --Value;
            }
        }
    }
}
