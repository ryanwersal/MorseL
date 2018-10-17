using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace MorseL.Sockets
{
    /// <summary>
    /// Singleton instance that manages all Connections.
    /// </summary>
    public class WebSocketConnectionManager : IWebSocketConnectionManager
    {
        private readonly ConcurrentDictionary<string, Connection> _connections = new ConcurrentDictionary<string, Connection>();

        public Connection GetConnectionById(string id)
        {
            return _connections.FirstOrDefault(p => p.Key == id).Value;
        }

        public Connection GetConnection(WebSocket socket)
        {
            return _connections.FirstOrDefault(p => ((WebSocketChannel)p.Value.Channel).Socket == socket).Value;
        }

        public ICollection<Connection> GetAll()
        {
            return _connections.Values;
        }

        public string GetId(Connection connection)
        {
            return _connections.FirstOrDefault(p => p.Value == connection).Key;
        }

        public Connection AddConnection(IChannel channel)
        {
            var connection = new Connection(CreateConnectionId(), channel);
            _connections.TryAdd(connection.Id, connection);
            return connection;
        }

        public bool HasConnection(string id)
        {
            return _connections.ContainsKey(id);
        }

        public async Task RemoveConnection(string id)
        {
            _connections.TryRemove(id, out var connection);
            await connection.DisposeAsync();
            connection = null;
        }

        private static string CreateConnectionId()
        {
            return Guid.NewGuid().ToString();
        }
    }
}
