using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using MorseL.Common;

namespace MorseL.Scaleout
{
    public class DefaultBackplane : IBackplane
    {
        private readonly IDictionary<string, string> _connections = new ConcurrentDictionary<string, string>();
        private readonly IDictionary<string, IDictionary<string, string>> _groups = new ConcurrentDictionary<string, IDictionary<string, string>>();
        public event OnMessageDelegate OnMessage;

        public Task OnClientConnectedAsync(string connectionId)
        {
            _connections.Add(connectionId, null);
            return Task.CompletedTask;
        }

        public Task OnClientDisconnectedAsync(string connectionId)
        {
            _connections.Remove(connectionId);
            return Task.CompletedTask;
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
            if (!_groups.ContainsKey(group)) {
                foreach (var connection in _groups[group]) {
                    await InvokeOnMessage(connection.Key, message);
                }
            }
        }

        public Task Subscribe(string group, string connectionId)
        {
            if (_groups.ContainsKey(group)) {
                var subscribers = _groups[group] = new ConcurrentDictionary<string, string>();
            }

            _groups[group].Add(connectionId, null);

            return Task.CompletedTask;
        }

        public Task Unsubscribe(string group, string connectionId)
        {
            if (_groups.ContainsKey(group)) {
                _groups[group].Remove(connectionId);

                if (_groups[group].Count == 0) {
                    _groups.Remove(group);
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
            if (OnMessage != null) {
                await OnMessage(connectionId, message)
                    .ConfigureAwait(false);
            }
        }
    }
}