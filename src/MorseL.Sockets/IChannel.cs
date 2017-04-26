using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MorseL.Common;

namespace MorseL.Sockets
{
    public interface IChannel
    {
        Task SendMessageAsync(Message message);
    }
}
