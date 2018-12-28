using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MorseL.Extensions;
using MorseL.Sockets.Middleware;

namespace MorseL.Shared.Tests
{
    public class SimpleMorseLServer<THub> : IDisposable where THub : Hub
    {
        private IWebHost _webHost;
        private PortInstance _portInstance;
        private readonly ILogger _logger;

        private Action<IServiceCollection, IMorseLBuilder> _serviceConfigurator;
        private Action<IApplicationBuilder, IServiceProvider> _applicationCongurator;

        public IServiceProvider Services => _webHost.Services;
        public int Port { get; private set; }
        public IPAddress Address { get; private set; }
        public bool UseHttps { get; private set; }
        public string Uri => $"{(UseHttps ? "wss" : "ws")}://{(Address == IPAddress.Any ? "localhost" : Address.ToString())}:{Port}/hub";

        public SimpleMorseLServer(Action<IServiceCollection, IMorseLBuilder> services = null, Action<IApplicationBuilder, IServiceProvider> configure = null, IMiddleware[] middleware = null, ILogger logger = null)
        {
            _serviceConfigurator = services;
            _applicationCongurator = configure;
            _logger = logger ?? NullLogger.Instance;
        }

        public async Task<IDisposable> Start(PortPool portPool, IPAddress address = null, bool useHttps = false)
        {
            _portInstance = await portPool.NextAsync();
            return await Start(_portInstance.Port, address, useHttps);
        }

        public async Task<IDisposable> Start(int port, IPAddress address = null, bool useHttps = false)
        {
            Address = address ?? IPAddress.Any;
            Port = port;
            UseHttps = useHttps;

            _logger.LogInformation($"Starting SimpleMorseLServer on {Uri}");

            _webHost = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.Listen(Address, Port, listenOptions =>
                    {
                        listenOptions.UseConnectionLogging();
                    });

                })
                .ConfigureServices(services =>
                {
                    var builder = services.AddMorseL();
                    _serviceConfigurator?.Invoke(services, builder);
                })
                .Configure(app =>
                {
                    _applicationCongurator?.Invoke(app, app.ApplicationServices);
                    app.UseWebSockets();
                    app.MapMorseLHub<THub>("/hub");
                })
                .Build();

            await _webHost.StartAsync();
            return this;
        }

        public void Dispose()
        {
            _portInstance?.Dispose();
            _webHost?.Dispose();
        }
    }
}
