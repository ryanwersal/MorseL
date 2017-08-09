using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MorseL.Common;
using MorseL.Common.Serialization;
using MorseL.Sockets.Middleware;

namespace MorseL.Sockets
{
    public static class ChannelExtensions
    {
        public static async Task SendMessageAsync(this IChannel channel, Message message)
        {
            if (channel.State != ChannelState.Open)
                throw new MorseLException($"The channel is not open; actual state is ({channel.State})");

            // TODO: Serializer settings? Usage is inconsistent in the entire solution.
            var serializedMessage = Json.SerializeObject(message);
            var bytes = Encoding.ASCII.GetBytes(serializedMessage);

            using (var stream = new MemoryStream(bytes))
            {
                await channel.SendAsync(stream);
            }
        }
    }
}
