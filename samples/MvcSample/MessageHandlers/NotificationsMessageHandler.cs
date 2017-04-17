using WebSocketManager;

namespace MvcSample.MessageHandlers
{
    public class NotificationsMessageHandler : Hub
    {
        public NotificationsMessageHandler(WebSocketConnectionManager webSocketConnectionManager) : base(webSocketConnectionManager)
        {
        }
    }
}