using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MorseL.Sockets.Middleware;

namespace MorseL.Sockets
{
    /// <summary>
    /// Singleton instance that manages all Connections.
    /// </summary>
    public class WebSocketConnectionManager
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

        public async Task RemoveConnection(string id)
        {
            _connections.TryRemove(id, out Connection connection);
            await connection.DisposeAsync();
        }

        private static string CreateConnectionId()
        {
            return Guid.NewGuid().ToString();
        }
    }
}
