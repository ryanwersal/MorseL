using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Builder.Internal;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MorseL;
using MorseL.Extensions;

namespace MorseL.Client.WebSockets.Tests
{
    public class SimpleMorseLServer<THub> where THub : Hub
    {
        private readonly IWebHost _webHost;

        public SimpleMorseLServer(IPAddress address, int port)
        {
            _webHost = new WebHostBuilder()
                .UseStartup<Startup>()
                .UseKestrel(options =>
                {
                    options.UseConnectionLogging();
                })
                .UseUrls($"http://{address}:{port}")
                .Build();
        }

        public IDisposable Start()
        {
            _webHost.Start();
            return _webHost;
        }

        public class Startup
        {
            public Startup(IHostingEnvironment env)
            {
            }

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddMorseL();
            }

            public void Configure(IApplicationBuilder app, IServiceProvider serviceProvider,
                ILoggerFactory loggerFactory)
            {
                app.UseWebSockets();
                app.MapMorseLHub<THub>("/hub");
            }
        }
    }
}
