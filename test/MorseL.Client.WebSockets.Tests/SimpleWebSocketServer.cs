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

namespace MorseL.Client.WebSockets.Tests
{
    public class SimpleWebSocketServer
    {
        private readonly IWebHost _webHost;

        public SimpleWebSocketServer(IPAddress address, int port)
        {
            _webHost = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.Listen(address, port, listenOptions =>
                    {
                        listenOptions.UseConnectionLogging();
                    });
                })
                .UseStartup<Startup>()
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
            }

            public void Configure(IApplicationBuilder app, IServiceProvider serviceProvider,
                ILoggerFactory loggerFactory)
            {
                app.UseWebSockets();
                app.Use(async (context, func) =>
                {
                    if (!context.WebSockets.IsWebSocketRequest) return;
                    var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
                    await socket.ReceiveAsync(new ArraySegment<byte>(new byte[1024]), CancellationToken.None);
                    await func();
                });
            }
        }
    }
}
