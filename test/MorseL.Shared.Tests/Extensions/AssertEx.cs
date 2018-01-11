using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace MorseL.Shared.Tests.Extensions
{
    public static class AssertEx
    {
        public static async Task ThrowsAsync<T>(Func<Task> action, Func<T, bool> validateException)
            where T : Exception
        {
            try
            {
                await action();
            }
            catch (Exception e)
            {
                Assert.IsType<T>(e);
                Assert.True(validateException(e as T));
            }
        }
    }
}
