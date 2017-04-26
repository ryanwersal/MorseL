using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MorseL.Scaleout;
using MorseL.Sockets;

namespace MorseL
{
    public static class MorseLExtensions
    {
        public static IServiceCollection AddMorseL(this IServiceCollection services, IScaleoutBackPlane scaleoutBackPlane = null)
        {
            services.AddSingleton(scaleoutBackPlane ?? new ScaleoutBackPlane());
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

        public static IApplicationBuilder MapMorseLHub<THub, TClient>(
            this IApplicationBuilder app, PathString path) where THub : Hub<TClient>
        {
            return app.Map(path, _app => _app.UseMiddleware<MorseLMiddleware>(typeof(HubWebSocketHandler<THub, TClient>)));
        }

        public static IApplicationBuilder MapMorseLHub<THub>(
            this IApplicationBuilder app, PathString path) where THub : Hub<IClientInvoker>
        {
            return app.Map(path, _app => _app.UseMiddleware<MorseLMiddleware>(typeof(HubWebSocketHandler<THub, IClientInvoker>)));
        }
    }
}
