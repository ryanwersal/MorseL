using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MorseL.Common;
using MorseL.Common.Serialization;

namespace MorseL.Sockets
{
    public static class ChannelExtensions
    {
        public static async Task SendMessageAsync(this IChannel channel, Message message)
        {
            // Don't send message to a channel that isn't open.
            if (channel.State != ChannelState.Open) return;

            // TODO: Serializer settings? Usage is inconsistent in the entire solution.
            var serializedMessage = MessageSerializer.SerializeObject(message);
            var bytes = Encoding.ASCII.GetBytes(serializedMessage);

            using (var stream = new MemoryStream(bytes))
            {
                await channel.SendAsync(stream);
            }
        }
    }
}
