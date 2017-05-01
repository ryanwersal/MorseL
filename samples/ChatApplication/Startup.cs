using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ChatApplication.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MorseL.Common;
using MorseL.Extensions;
using MorseL.Sockets;
using MorseL.Sockets.Middleware;

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
            services
                .AddMorseL()
                .AddMiddleware<Transform>(ServiceLifetime.Scoped);
        }

        private class Transform : IMiddleware
        {
            private static int count = 0;
            private int id = count++;

            public async Task SendAsync(ConnectionContext context, MiddlewareDelegate next)
            {
                using (var reader = new StreamReader(context.Stream))
                {
                    var data = Encoding.UTF8.GetBytes(await reader.ReadToEndAsync());
                    using (var txStream = new MemoryStream(Encoding.UTF8.GetBytes(Convert.ToBase64String(data))))
                    {
                        await next(new ConnectionContext(context.Connection, txStream));
                    }
                }
            }

            public async Task ReceiveAsync(ConnectionContext context, MiddlewareDelegate next)
            {
                using (var reader = new StreamReader(context.Stream))
                {
                    var data = await reader.ReadToEndAsync();
                    using (var rxStream = new MemoryStream(Convert.FromBase64String(data)))
                    {
                        await next(new ConnectionContext(context.Connection, rxStream));
                    }
                }
            }
        }
    }
}
