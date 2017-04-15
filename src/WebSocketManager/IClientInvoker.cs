using System.Threading.Tasks;
using WebSocketManager.Common;

namespace WebSocketManager
{
    public interface IClientInvoker
    {
        Task InvokeAsync(string methodName, params object[] args);
        Task SendMessageAsync(Message message);
    }
}
