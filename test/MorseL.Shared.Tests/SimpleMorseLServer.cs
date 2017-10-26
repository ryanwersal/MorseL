using System;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MorseL.Extensions;
using MorseL.Sockets.Middleware;

namespace MorseL.Shared.Tests
{
    public class SimpleMorseLServer<THub> where THub : Hub
    {
        private readonly IWebHost _webHost;
        private static Action<IServiceCollection, IMorseLBuilder> _serviceConfigurator;
        private static Action<IApplicationBuilder, IServiceProvider> _applicationCongurator;

        public SimpleMorseLServer(IPAddress address, int port, Action<IServiceCollection, IMorseLBuilder> services = null, Action<IApplicationBuilder, IServiceProvider> configure = null, IMiddleware[] middleware = null)
        {
            _serviceConfigurator = services;
            _applicationCongurator = configure;
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
                var builder = services.AddMorseL();
                _serviceConfigurator?.Invoke(services, builder);
            }

            public void Configure(IApplicationBuilder app, IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
            {
                _applicationCongurator?.Invoke(app, serviceProvider);
                app.UseWebSockets();
                app.MapMorseLHub<THub>("/hub");
            }
        }
    }
}
