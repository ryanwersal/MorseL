using System;
using Microsoft.Extensions.Logging;

namespace MorseL.Diagnostics
{
    public static class LoggerExtensions
    {
        public static IDisposable Tracer(this ILogger logger, string format, params object[] args)
        {
            return new Tracer(logger, format, args);
        }
    }
}
