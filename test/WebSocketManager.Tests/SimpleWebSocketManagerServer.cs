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
using WebSocketManager;

namespace AsyncWebSocketClient.Tests
{
    public class SimpleWebSocketManagerServer<THub> where THub : Hub
    {
        private readonly IWebHost _webHost;

        public SimpleWebSocketManagerServer(IPAddress address, int port)
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
                services.AddWebSocketManager();
            }

            public void Configure(IApplicationBuilder app, IServiceProvider serviceProvider,
                ILoggerFactory loggerFactory)
            {
                app.UseWebSockets();
                app.MapWebSocketManagerHub<THub>("/hub");
            }
        }
    }
}
