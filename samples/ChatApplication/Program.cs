using System.IO;
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
                .UseUrls("http://0.0.0.0:5000", "https://0.0.0.0:5001")
                .UseKestrel(options =>
                {
                    options.UseHttps(new HttpsConnectionFilterOptions()
                    {
                        ServerCertificate = new X509Certificate2("iis.cer")
                    });
                })
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}
