using System.Threading.Tasks;
using MorseL.Common;

namespace MorseL.Scaleout
{
    public delegate Task OnMessageDelegate(string connectionId, Message message);

    public interface IBackplane
    {
        Task OnClientConnectedAsync(string connectionId, OnMessageDelegate messageDelegate);
        Task OnClientDisconnectedAsync(string connectionId);

        Task DisconnectClientAsync(string connectionId);

        Task Subscribe(string group, string connectionId);
        Task SubscribeAll(string group);

        Task Unsubscribe(string group, string connectionId);
        Task UnsubscribeAll(string group);

        Task SendMessageAsync(string connectionId, Message message);
        Task SendMessageAllAsync(Message message);
        Task SendMessageGroupAsync(string group, Message message);
    }
}
