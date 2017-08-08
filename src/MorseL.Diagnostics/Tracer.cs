using System;
using Microsoft.Extensions.Logging;

namespace MorseL.Diagnostics
{
    public class Tracer : IDisposable
    {
        private readonly DateTime _startTime = DateTime.Now;
        private readonly ILogger _logger;
        private readonly string _message;

        public Tracer(ILogger logger, string format, params object[] args)
        {
            _logger = logger;
            _message = string.Format(format, args);
            _logger.LogTrace($"<tracer> [Start]\t\"{_message}\"");
        }

        public void Dispose()
        {
            _logger.LogTrace($"<tracer> [End]\t\"{_message}\" Elapsed: {(DateTime.Now - _startTime).TotalMilliseconds}ms");
        }
    }
}