using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using WebSocketManager;

namespace ChatApplication
{
    public class Startup
    {
        public void Configure(IApplicationBuilder app, IServiceProvider serviceProvider)
        {
            app.UseWebSockets();
            app.MapWebSocketManagerHub<ChatHandler>("/chat");

            app.UseStaticFiles();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddWebSocketManager();
        }
    }
}
