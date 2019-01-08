using System;
using Host.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MorseL.Extensions;
using MorseL.Scaleout;
using MorseL.Scaleout.Redis;
using Serilog;
using StackExchange.Redis;

namespace Host
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.ColoredConsole(outputTemplate: "{Timestamp:u} (Host:{ClientName}) {Message}{NewLine}{Exception}")
                .CreateLogger();
        }

        public void Configure(IApplicationBuilder app, IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
        {
            app.UseWebSockets();
            app.MapMorseLHub<HostHub>("/hub");

            app.UseStaticFiles();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMorseL();

            services.AddSingleton<IBackplane, RedisBackplane>();
            services.Configure<ConfigurationOptions>(o =>
            {
                o.EndPoints.Add("localhost:6379");
            });

            services.AddSingleton(Log.Logger);
        }
    }
}
