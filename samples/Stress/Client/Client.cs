using System;
using System.Linq;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Bogus;
using ByteSizeLib;
using MorseL.Client;
using Serilog;

namespace Client
{
    public class Client : IDisposable
    {
        private IConnection _connection;
        private readonly string _name;
        private readonly string _host;
        private readonly int _port;
        private readonly string _protocol;
        private readonly bool _useSsl;
        private readonly X509Certificate2 _clientCertificate;

        private static readonly Faker Faker = new Faker();

        private static readonly TimeSpan MaxExpectResponseDelay = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan MaxParallelExpectResponseDelay = TimeSpan.FromSeconds(1);

        public Client(string name, string host, int port, bool useSsl, string clientCert, string clientCertPassphrase)
        {
            _name = name;
            _host = host;
            _port = port;
            _protocol = _useSsl ? "wss" : "ws";
            _useSsl = useSsl;
            _clientCertificate = _useSsl && clientCert != null ? new X509Certificate2(clientCert, clientCertPassphrase) : null;
        }

        public async Task StartAsync()
        {
            while (!(_connection?.IsConnected ?? false))
            {
                try
                {
                    CreateConnection();
                    await _connection.StartAsync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Unable to connect. Retrying...");
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
            }

            await _connection.Invoke<object>("Hello", _name);

            var actions = new Func<string, IConnection, Task>[]
            {
                Ping,
                Respond,
                ExpectResponse,
                ExpectResponseParallel,
            };

            while (true)
            {
                await Faker.PickRandom(actions).Invoke(_name, _connection);
            }
        }

        private void CreateConnection()
        {
            Log.Information("Creating connection");

            _connection = new Connection(
                $"{_protocol}://{_host}:{_port}/hub",
                _name,
                morselOptions => { },
                socketOptions => { },
                securityOptions =>
                {
                    if (_useSsl)
                    {
                        securityOptions.Certificates = new X509Certificate2Collection(_clientCertificate);
                        // NOTE: Don't actually enable these in production code - this is done for the sake of
                        // using self signed certificates in a demo/sample.
                        securityOptions.AllowNameMismatchCertificate = true;
                        securityOptions.AllowUnstrustedCertificate = true;
                        securityOptions.EnabledSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;
                    }
                });
            
            for (var i = 0; i < 10; ++i)
            {
                RegisterLoremHandler(i, _connection);
            }
        }

        private static DateTimeOffset RespondStartTime;

        private static void RegisterLoremHandler(int num, IConnection connection)
        {
            var name = $"Lorem-{num}";
            connection.On<int, string>(name, (index, data) =>
            {
                var bytes = data.Length * sizeof(char);
                Log.Debug("Received {Function} for {Index} of {Size}", name, index, ByteSize.FromBytes(bytes).ToString());

                if (index == 999)
                {
                    var endTime = DateTimeOffset.UtcNow;
                    var duration = endTime - RespondStartTime;
                    Log.Information("{Function} finished at {EndTime} ({Duration})", nameof(Respond), endTime, duration);
                }
            });
        }

        public void Dispose()
        {
            _clientCertificate?.Dispose();
            _connection.DisposeAsync().Wait();
        }

        private static async Task Ping(string name, IConnection connection)
        {
            Log.Information("Invoking {Function}", nameof(Ping));
            await connection.Invoke<object>(nameof(Ping));
        }

        private static Task Respond(string name, IConnection connection)
        {
            Log.Information("Invoking {Function}", nameof(Respond));

            var messageCount = Faker.Random.Int(100, 300);
            var paragraphCount = Faker.Random.Int(25, 250);

            RespondStartTime = DateTimeOffset.UtcNow;

            // Don't wait for completion. Send and we will get invoked by the server.
            var _ = connection.Invoke(nameof(Respond), messageCount, paragraphCount);

            Log.Information("{Function} started at {StartTime}", nameof(Respond), RespondStartTime);

            return Task.CompletedTask;
        }

        private static async Task ExpectResponse(string name, IConnection connection)
        {
            var input = Faker.Lorem.Paragraphs();
            Log.Information("Requesting {Function} of {Length} characters.", nameof(ExpectResponse), input.Length);

            var delay = Faker.Date.Timespan(MaxExpectResponseDelay);
            if (!input.Equals(await connection.Invoke<string>(nameof(ExpectResponse), "single", input, delay.TotalMilliseconds)))
            {
                Log.Error("{Function} failed", nameof(ExpectResponse));
            }
        }

        private static async Task ExpectResponseParallel(string name, IConnection connection)
        {
            var parallelCount = Faker.Random.Int(2, 10);
            var inputs = Enumerable.Range(0, parallelCount).Select(i => Faker.Lorem.Paragraphs()).ToArray();
            Log.Information("Requesting {ParallelCount} parallel {Function} requests.", parallelCount, nameof(ExpectResponse));

            var tasks = inputs.Select((s, i) =>
                connection.Invoke<string>(
                    nameof(ExpectResponse),
                    $"{i + 1}/{parallelCount}",
                    s,
                    Faker.Date.Timespan(MaxParallelExpectResponseDelay).TotalMilliseconds));

            var result = await Task.WhenAll(tasks);
            if (result.Where((s, i) => !inputs[i].Equals(s)).Any())
            {
                Log.Error("{Function} failed", nameof(ExpectResponseParallel));
            }
        }
    }
}
