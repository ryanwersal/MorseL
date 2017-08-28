using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MorseL.Scaleout;
using MorseL.Sockets;
using MorseL.Sockets.Middleware;

namespace MorseL.Extensions
{
    public static class MorseLExtensions
    {
        public static IMorseLBuilder AddMorseL(this IServiceCollection services, Action<MorseLOptions> options = null)
        {
            services.AddSingleton<IBackplane, DefaultBackplane>();
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

            services.Configure(options ?? (o => { }));

            return new MorseLBuilder(services);
        }

        /// <summary>
        /// Add middleware that MorseL will invoke during web socket data transmission. Note: The
        /// lifetime passed in determines how and when the created middleware is created and exists.
        /// </summary>
        /// <typeparam name="TMiddleware">The type of the middleware to be invoked.</typeparam>
        /// <param name="builder">The MorseLBuilder object</param>
        /// <param name="lifetime">The lifetime the middleware should be created and adhere to.</param>
        /// <returns></returns>
        public static IMorseLBuilder AddMiddleware<TMiddleware>(this IMorseLBuilder builder, ServiceLifetime lifetime) where TMiddleware : IMiddleware
        {
            builder.Services.TryAddEnumerable(ServiceDescriptor.Describe(typeof(IMiddleware), typeof(TMiddleware), lifetime));
            return builder;
        }

        public static IApplicationBuilder MapMorseLHub<THub, TClient>(
            this IApplicationBuilder app, PathString path) where THub : Hub<TClient>
        {
            return app.Map(path, a => a.UseMiddleware<MorseLHttpMiddleware>(typeof(HubWebSocketHandler<THub, TClient>)));
        }

        public static IApplicationBuilder MapMorseLHub<THub>(
            this IApplicationBuilder app, PathString path) where THub : Hub<IClientInvoker>
        {
            return app.Map(path, a => a.UseMiddleware<MorseLHttpMiddleware>(typeof(HubWebSocketHandler<THub, IClientInvoker>)));
        }
    }
}
