using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using MorseL.Sockets.Middleware;

namespace MorseL.Extensions
{
    public class MorseLBuilder : IMorseLBuilder
    {
        public IServiceCollection Services { get; }

        public MorseLBuilder(IServiceCollection services)
        {
            Services = services;
        }
    }
}
