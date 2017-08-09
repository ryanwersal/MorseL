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
        private static Action<IServiceCollection> ServiceConfigurator;
        private static Action<IApplicationBuilder, IServiceProvider> ApplicationCongurator;

        public SimpleMorseLServer(IPAddress address, int port, Action<IServiceCollection> services = null, Action<IApplicationBuilder, IServiceProvider> configure = null)
        {
            ServiceConfigurator = services;
            ApplicationCongurator = configure;
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
                ServiceConfigurator?.Invoke(services);
            }

            public void Configure(IApplicationBuilder app, IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
            {
                ApplicationCongurator?.Invoke(app, serviceProvider);
                app.UseWebSockets();
                app.MapMorseLHub<THub>("/hub");
            }
        }
    }
}
