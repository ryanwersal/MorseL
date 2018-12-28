using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions.Internal;
using Xunit.Abstractions;

namespace MorseL.Shared.Tests
{
    public class TestOutputHelperLogger : ILogger
    {
        private ITestOutputHelper _testOutputHelper;

        public TestOutputHelperLogger(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            _testOutputHelper.WriteLine($"[{logLevel.ToString().ToUpper()}]: {message}");

            if (exception != null)
            {
                do
                {
                    _testOutputHelper.WriteLine($"Exception: {exception.Message}");
                    _testOutputHelper.WriteLine(exception.StackTrace);
                } while ((exception = exception.InnerException) != null);
            }
        }
    }
}
