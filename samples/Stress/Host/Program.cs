using System.IO;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.CommandLineUtils;

namespace Host
{
    class Program
    {
        static void Main(string[] args)
        {
            var app = new CommandLineApplication();
            app.Name = "MorseL Stress Test Host";
            var host = app.Option("-H|--host", "The host address to bind to.", CommandOptionType.SingleValue, false);
            var port = app.Option("-p|--port", "The host port to bind to.", CommandOptionType.SingleValue, false);
            var securePort = app.Option("-P|--secure-port", "The host port to bind to.", CommandOptionType.SingleValue, false);

            app.OnExecute(() =>
            {
                var hostValue = host.HasValue() ? host.Value() : "localhost";
                var portValue = port.HasValue() ? port.Value() : "5000";
                var securePortValue = securePort.HasValue() ? securePort.Value() : "5001";

                var webHost = new WebHostBuilder()
                    .UseUrls($"http://{hostValue}:{portValue}", $"https://{hostValue}:{securePortValue}")
                    .UseKestrel(options =>
                    {
                        options.UseConnectionLogging();
                        options.UseHttps(new HttpsConnectionFilterOptions()
                        {
                            ServerCertificate = new X509Certificate2("server.pfx"),
                            ClientCertificateMode = ClientCertificateMode.AllowCertificate,
                            SslProtocols = SslProtocols.Tls | SslProtocols.Tls11,
                            CheckCertificateRevocation = false,
                            ClientCertificateValidation = (certificate2, chain, arg3) => true
                        });
                    })
                    .UseContentRoot(Directory.GetCurrentDirectory())
                    .UseStartup<Startup>()
                    .Build();

                webHost.Run();
                return 0;
            });

            app.Execute(args);
        }
    }
}