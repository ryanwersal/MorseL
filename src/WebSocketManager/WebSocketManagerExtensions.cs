using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace WebSocketManager
{
    public static class WebSocketManagerExtensions
    {
        public static IServiceCollection AddWebSocketManager(this IServiceCollection services)
        {
            services.AddSingleton<WebSocketConnectionManager>();

            foreach (var type in Assembly.GetEntryAssembly().ExportedTypes)
            {
                if (type.GetTypeInfo().BaseType == typeof(Hub))
                {
                    services.AddTransient(type);
                }
            }

            return services;
        }

        public static IApplicationBuilder MapWebSocketManagerHub<T>(
            this IApplicationBuilder app, PathString path) where T : Hub
        {
            return app.Map(path, _app => _app.UseMiddleware<WebSocketManagerMiddleware>(typeof(T)));
        }
    }
}
