using System.IO;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;

namespace ChatApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .UseUrls("http://localhost:5000", "https://localhost:5001")
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

            host.Run();
        }
    }
}
