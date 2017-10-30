using System.IO;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using System.Net;

namespace ChatApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.Listen(IPAddress.Loopback, 5000, listenOptions =>
                    {
                        listenOptions.UseConnectionLogging();
                    });
                    options.Listen(IPAddress.Loopback, 5001, listenOptions =>
                    {
                        listenOptions
                            .UseConnectionLogging()
                            .UseHttps(new HttpsConnectionAdapterOptions
                            {
                                ServerCertificate = new X509Certificate2("server.pfx"),
                                ClientCertificateMode = ClientCertificateMode.AllowCertificate,
                                SslProtocols = SslProtocols.Tls | SslProtocols.Tls11,
                                CheckCertificateRevocation = false,
                                ClientCertificateValidation = (certificate2, chain, arg3) => true
                            });
                    });
                })
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}
