using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MorseL.Common;

[assembly: InternalsVisibleTo("MorseL.Scaleout.Tests")]
namespace MorseL.Scaleout
{
    public class DefaultBackplane : IBackplane
    {
        private readonly IDictionary<string, OnMessageDelegate> _connections = new ConcurrentDictionary<string, OnMessageDelegate>();
        private readonly IDictionary<string, IDictionary<string, object>> _groups = new ConcurrentDictionary<string, IDictionary<string, object>>();
        private readonly IDictionary<string, IDictionary<string, object>> _subscriptions = new ConcurrentDictionary<string, IDictionary<string, object>>();

        internal int OnMessageCount => _connections.Count();

        public Task OnClientConnectedAsync(string connectionId, OnMessageDelegate onMessageDelegate)
        {
            _connections.Add(connectionId, onMessageDelegate);

            return Task.CompletedTask;
        }

        public async Task OnClientDisconnectedAsync(string connectionId)
        {
            _connections.Remove(connectionId);

            // Unsubscribe from groups
            if (_subscriptions.ContainsKey(connectionId))
            {
                foreach (var group in _subscriptions[connectionId].Keys.ToArray())
                {
                    await Unsubscribe(group, connectionId);
                }
            }
        }

        public async Task DisconnectClientAsync(string connectionId)
        {
            await SendMessageAsync(connectionId, new Message
            {
                MessageType = MessageType.Disconnect
            });
        }

        public async Task SendMessageAllAsync(Message message)
        {
            foreach(var connection in _connections.Keys) {
                await InvokeOnMessage(connection, message);
            }
        }

        public async Task SendMessageAsync(string connectionId, Message message)
        {
            await InvokeOnMessage(connectionId, message);
        }

        public async Task SendMessageGroupAsync(string group, Message message)
        {
            if (_groups.ContainsKey(group)) {
                foreach (var connection in _groups[group]) {
                    await InvokeOnMessage(connection.Key, message);
                }
            }
        }

        public Task Subscribe(string group, string connectionId)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            if (connectionId == null) throw new ArgumentNullException(nameof(connectionId));

            if (!_groups.ContainsKey(group)) {
                _groups[group] = new ConcurrentDictionary<string, object>();
            }

            if (!_subscriptions.ContainsKey(connectionId))
            {
                _subscriptions[connectionId] = new ConcurrentDictionary<string, object>();
            }

            _groups[group].Add(connectionId, null);
            _subscriptions[connectionId].Add(group, null);

            return Task.CompletedTask;
        }

        public Task Unsubscribe(string group, string connectionId)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            if (connectionId == null) throw new ArgumentNullException(nameof(connectionId));

            if (_groups.ContainsKey(group)) {
                _groups[group].Remove(connectionId);

                if (_groups[group].Count == 0) {
                    _groups.Remove(group);
                }
            }

            if (_subscriptions.ContainsKey(connectionId))
            {
                _subscriptions[connectionId].Remove(group);

                if (_subscriptions[connectionId].Count == 0)
                {
                    _subscriptions.Remove(connectionId);
                }
            }

            return Task.CompletedTask;
        }

        public async Task SubscribeAll(string group)
        {
            foreach (var connection in _connections) {
                await Subscribe(group, connection.Key);
            }
        }

        public async Task UnsubscribeAll(string group)
        {
            foreach (var connection in _connections) {
                await Unsubscribe(group, connection.Key);
            }
        }

        private async Task InvokeOnMessage(string connectionId, Message message) {
            if (_connections.TryGetValue(connectionId, out var messageDelegate) && messageDelegate != null) {
                await messageDelegate.Invoke(connectionId, message)
                    .ConfigureAwait(false);
            }
        }

        internal IDictionary<string, IDictionary<string, object>> Groups => _groups;
        internal IDictionary<string, IDictionary<string, object>> Subscriptions => _subscriptions;
    }
}
