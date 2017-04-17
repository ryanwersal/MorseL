using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WebSocketManager.Sockets;

namespace WebSocketManager
{
    public static class WebSocketManagerExtensions
    {
        public static IServiceCollection AddWebSocketManager(this IServiceCollection services)
        {
            services.AddSingleton<WebSocketConnectionManager>();
            services.AddSingleton(typeof(HubWebSocketHandler<>), typeof(HubWebSocketHandler<>));
            services.AddScoped(typeof(IHubActivator<,>), typeof(DefaultHubActivator<,>));

            foreach (var type in Assembly.GetEntryAssembly().ExportedTypes)
            {
                if (type.GetTypeInfo().BaseType == typeof(Hub))
                {
                    services.AddTransient(type);
                }
            }

            return services;
        }

        public static IApplicationBuilder MapWebSocketManagerHub<THub, TClient>(
            this IApplicationBuilder app, PathString path) where THub : Hub<TClient>
        {
            return app.Map(path, _app => _app.UseMiddleware<WebSocketManagerMiddleware>(typeof(HubWebSocketHandler<THub, TClient>)));
        }

        public static IApplicationBuilder MapWebSocketManagerHub<THub>(
            this IApplicationBuilder app, PathString path) where THub : Hub<IClientInvoker>
        {
            return app.Map(path, _app => _app.UseMiddleware<WebSocketManagerMiddleware>(typeof(HubWebSocketHandler<THub, IClientInvoker>)));
        }
    }
}
