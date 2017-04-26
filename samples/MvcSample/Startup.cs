using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MvcSample.Hubs;
using MorseL;

namespace MvcSample
{
    public class Startup
    {
        public void Configure(IApplicationBuilder app, IServiceProvider serviceProvider)
        {
            app.UseStaticFiles();
            app.UseWebSockets();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "api/{controller}/{action}/{id?}"
                );
            });

            app.MapMorseLHub<NotificationsMessageHub>("/notifications");
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddMorseL();
        }
    }
}