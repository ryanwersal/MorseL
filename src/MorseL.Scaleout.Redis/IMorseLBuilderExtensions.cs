using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MorseL.Extensions;
using StackExchange.Redis;

namespace MorseL.Scaleout.Redis
{
    public static class IMorseLBuilderExtensions
    {
        public static IMorseLBuilder AddRedisBackplane(this IMorseLBuilder builder, Action<ConfigurationOptions> options) {
            builder.Services.Configure<ConfigurationOptions>(options);
            builder.Services.AddSingleton<IBackplane, RedisBackplane>();
            return builder;
        }
    }
}