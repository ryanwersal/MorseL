using System.Security.Claims;

namespace WebSocketManager
{
    public class CallerContext
    {
        public Connection Connection { get; }
        public string ConnectionId => Connection.Id;
        public ClaimsPrincipal User => Connection.User;

        public CallerContext(Connection connection)
        {
            Connection = connection;
        }
    }
}
