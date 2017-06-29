using System;
using System.Diagnostics;
using Microsoft.Extensions.CommandLineUtils;
using Serilog;

namespace Client
{
    class Program
    {
        static void Main(string[] args)
        {
            var app = new CommandLineApplication();
            app.Name = "MorseL Stress Test Conductor";
            var name = app.Option("-n|--name", "The name of the client.", CommandOptionType.SingleValue, false);
            var host = app.Option("-H|--host", "The host address to bind to.", CommandOptionType.SingleValue, false);
            var port = app.Option("-p|--port", "The host port to bind to.", CommandOptionType.SingleValue, false);
            var ssl = app.Option("-S|--use-ssl", "Use WSS protocol", CommandOptionType.SingleValue, false);
            var clientCert = app.Option("--client-certificate", "A client certificate to use", CommandOptionType.SingleValue, false);
            var clientCertPassphrase = app.Option("--client-certificate-passphrae", "A client certificate passphrase to use", CommandOptionType.SingleValue, false);

            app.OnExecute(async () =>
            {
                Log.Logger = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .WriteTo.ColoredConsole(outputTemplate: "{Timestamp:u} (Client:{ClientName}) {Message}{NewLine}{Exception}")
                    .Enrich.WithProperty("ClientName", name.Value())
                    .CreateLogger();

                var hostValue = host.HasValue() ? host.Value() : "localhost";
                var portValue = int.Parse(port.HasValue() ? port.Value() : "5000");
                var useSsl = bool.Parse(ssl.HasValue() ? ssl.Value() : "false");

                await new Client(name.Value(), hostValue, portValue, useSsl, clientCert.Value(), clientCertPassphrase.Value()).StartAsync();
                return 0;
            });

            app.Execute(args);
        }
    }
}