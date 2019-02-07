using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MorseL.Diagnostics
{
    public static class IntrospectionStream
    {
        public static async Task<(Stream Stream, string Data)> Introspect(Stream stream)
        {
            using (var streamReader = new StreamReader(stream))
            {
                var data = await streamReader.ReadToEndAsync();

                return (new MemoryStream(Encoding.UTF8.GetBytes(data)), data);
            }
        }
    }
}
