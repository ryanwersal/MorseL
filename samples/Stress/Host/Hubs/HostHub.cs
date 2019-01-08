using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bogus;
using MorseL;
using MorseL.Sockets;
using Serilog;

namespace Host.Hubs
{
    public class HostHub : Hub
    {
        private ILogger _logger;
        private ILogger Logger => _logger.ForContext("ClientName", ConnectionName());

        private static readonly Faker Faker = new Faker();

        private static readonly Dictionary<string, string> ConnectionNames = new Dictionary<string, string>();
        private static readonly Dictionary<string, Counter> Counts = new Dictionary<string, Counter>();

        public HostHub(ILogger logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync(Connection connection)
        {
            _logger.Information("Connection established with {ConnectionId}", connection.Id);
            await Client.Subscribe(Context.ConnectionId);
        }

        public void Hello(string name)
        {
            ConnectionNames[Context.ConnectionId] = name;
        }

        public void Ping()
        {
            if (Count(nameof(Ping)).Increment() % 100 == 0)
            {
                Logger.Information("100 Ping's received");
            }
        }

        public async Task<string> ExpectResponse(string id, string input, int delay)
        {
            Logger.Information("Fielding ExpectResponse request ({Id}).", id);
            await Task.Delay(delay);
            Logger.Information("Completed ExpectResponse request ({Id}).", id);
            return input;
        }

        public async Task Respond(int messageCount, int paragraphCount)
        {
            using (new TransactionLogger(nameof(Respond)))
            {
                var tasks = Enumerable.Range(0, messageCount)
                    .Select(i => (Index: i, Method: $"Lorem-{Faker.Random.Int(0, 9)}", Payload: Faker.Lorem.Paragraphs(paragraphCount)))
                    .Select(d => Groups.Group(Context.ConnectionId).InvokeAsync(d.Method, d.Index, d.Payload));

                await Task.WhenAll(tasks);
            }
        }

        public Task OnDisconnectedAsync(Connection connection)
        {
            _logger.Information("Connection terminated with {ConnectionName}", ConnectionName());
            return Task.CompletedTask;
        }

        private string ConnectionName()
        {
            return ConnectionNames.ContainsKey(Context.ConnectionId) ? 
                ConnectionNames[Context.ConnectionId] : 
                Context.ConnectionId;
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

        private class TransactionLogger : IDisposable
        {
            private readonly DateTimeOffset startTime;
            private readonly string transactionName;

            public TransactionLogger(string name)
            {
                transactionName = name;
                startTime = DateTimeOffset.UtcNow;
                Log.Information("Starting {Name} at {Timestamp}", transactionName, startTime);
            }

            public void Dispose()
            {
                var endTime = DateTimeOffset.UtcNow;
                var duration = endTime - startTime;
                Log.Information("Finishing {Name} at {Timestamp} ({Duration})",
                    transactionName, endTime, duration);
            }
        }
    }
}
