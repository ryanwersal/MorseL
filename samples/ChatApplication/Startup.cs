using System;
using ChatApplication.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MorseL;

namespace ChatApplication
{
    public class Startup
    {
        public void Configure(IApplicationBuilder app, IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
        {
            loggerFactory
                .AddConsole()
                .AddDebug();

            app.UseWebSockets();
            app.MapMorseLHub<ChatHub>("/chat");

            app.UseStaticFiles();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMorseL();
        }
    }
}
