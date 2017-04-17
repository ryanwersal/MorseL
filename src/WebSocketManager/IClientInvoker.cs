using System.Threading.Tasks;
using WebSocketManager.Common;

namespace WebSocketManager
{
    // TODO : Make this more like IClientProxy
    public interface IClientInvoker
    {
        Task InvokeAsync(string methodName, params object[] args);
        Task SendMessageAsync(Message message);
    }
}
