using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Client.Helper;
using MorseL.Client;
using Serilog;

namespace Client
{
    public class Client : IDisposable
    {
        private Connection _connection;
        private readonly string _name;
        private readonly string _host;
        private readonly int _port;
        private readonly string _protocol;
        private readonly bool _useSsl;
        private readonly X509Certificate2 _clientCertificate;

        private static readonly Random Random = new Random((int) DateTime.Now.Ticks);

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
            await Task.Delay(1000);

            CreateConnection();

            await _connection.StartAsync();

            await _connection.Invoke<object>("Hello", _name);

            var random = new Random();

            while (true)
            {
                await Actions[Random.Next(0, Actions.Length)](_name, _connection);
            }
        }

        private void CreateConnection()
        {
            Log.Information("Creating connection");

            _connection = new Connection($"{_protocol}://{_host}:{_port}/hub", _name, option =>
            {
            }, option =>
            {
                if (_useSsl)
                {
                    option.Certificates = new X509Certificate2Collection(_clientCertificate);
                    option.AllowNameMismatchCertificate = true;
                    option.AllowUnstrustedCertificate = true;
                    option.EnabledSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;
                }
            });
        }

        public void Dispose()
        {
            _clientCertificate?.Dispose();
            _connection.DisposeAsync().Wait();
        }

        private readonly Func<string, Connection, Task>[] Actions =
        {
            async (name, connection) =>
            {
                Log.Information("Invoking Ping");
                await connection.Invoke<object>("Ping");
            },
            async (name, connection) =>
            {
                var input = LoremIpsum.Generate(1, 1000, 1, 100, 1);
                Log.Information($"Requesting LoremIpsum of {input.Length} characters.");
                if (!input.Equals(await connection.Invoke<string>("ExpectResponse", "single", input, Random.Next(0, 10000))))
                {
                    Log.Error("ExpectResponse failed");
                }
            },
            async (name, connection) =>
            {
                var parallelCount = Random.Next(2, 10);
                var inputs = Enumerable.Range(0, parallelCount).Select(i => LoremIpsum.Generate(1, 1000, 1, 100, 1)).ToArray();
                Log.Information($"Requesting {parallelCount} parallel LoremIpsum requests.");
                var tasks = inputs.Select((s, i) => connection.Invoke<string>("ExpectResponse", $"{i + 1}/{parallelCount}", s, Random.Next(0, 1000)));
                var result = await Task.WhenAll(tasks);
                if (result.Where((s, i) => !inputs[i].Equals(s)).Any())
                {
                    Log.Error("Parallel ExpectResponse failed");
                }
            }
        };
    }
}
