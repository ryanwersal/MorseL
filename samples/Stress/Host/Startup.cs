using System;
using Host.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MorseL.Extensions;
using Serilog;

namespace Host
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            // Configure the Serilog pipeline
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.ColoredConsole(outputTemplate: "{Timestamp:u} (Host:{ClientName}) {Message}{NewLine}{Exception}")
                .CreateLogger();
        }

        public void Configure(IApplicationBuilder app, IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
        {
//            loggerFactory
//                .AddConsole()
//                .AddSerilog();

            app.UseWebSockets();
            app.MapMorseLHub<HostHub>("/hub");

            app.UseStaticFiles();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMorseL();
            services.AddSingleton(Log.Logger);
        }
    }
}
