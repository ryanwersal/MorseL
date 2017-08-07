using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MorseL.Common;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace MorseL.Scaleout.Redis
{
    public class RedisBackplane : IBackplane
    {
        private const string RedisKey_AllGroup = "redis-backplane:group:all";
        private const string RedisKey_ConnectionPrefix = "redis-backplane:";
        private const string RedisKey_GroupPrefix = "redis-backplane:group:";

        private readonly Lazy<ConnectionMultiplexer> _lazyConnection;
        private ConnectionMultiplexer _connection => _lazyConnection.Value;
        private IDatabase _cache => _connection.GetDatabase();
        private readonly IDictionary<string, string> _connections = new ConcurrentDictionary<string, string>();
        private readonly IDictionary<string, IDictionary<string, string>> _groups = new ConcurrentDictionary<string, IDictionary<string, string>>();

        public RedisBackplane(IOptions<ConfigurationOptions> options)
        {
            _lazyConnection = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(options.Value));

            // TODO : Offload this from the constructor
            var subscriber = _cache.Multiplexer.GetSubscriber();
            subscriber.Subscribe(
                RedisKey_AllGroup,
                async (channel, message) => {
                    foreach(var connection in _connections.Keys) {
                        await InvokeOnMessage(
                            connection,
                            message
                        ).ConfigureAwait(false);
                    }
                }
            );
        }

        public event OnMessageDelegate OnMessage;

        public async Task OnClientConnectedAsync(string connectionId)
        {
            _connections.Add(connectionId, null);

            var subscriber = _cache.Multiplexer.GetSubscriber();

            // Subscribe to messages for the client
            await subscriber.SubscribeAsync(
                GetRedisKeyForConnectionId(connectionId),
                async (channel, message) => await InvokeOnMessage(connectionId, message)
            ).ConfigureAwait(false);
        }

        public async Task OnClientDisconnectedAsync(string connectionId)
        {
            _connections.Remove(connectionId);

            var subscriber = _cache.Multiplexer.GetSubscriber();

            // Unsubscribe for messages to the client
            await subscriber.UnsubscribeAsync(
                GetRedisKeyForConnectionId(connectionId)
            ).ConfigureAwait(false);
        }

        public async Task SendMessageAsync(string connectionId, Message message)
        {
            var subscriber = _cache.Multiplexer.GetSubscriber();

            // Publish the message
            await subscriber.PublishAsync(
                GetRedisKeyForConnectionId(connectionId),
                JsonConvert.SerializeObject(message)
            ).ConfigureAwait(false);
        }

        public async Task SendMessageAllAsync(Message message)
        {
            var subscriber = _cache.Multiplexer.GetSubscriber();

            // Publish the message
            await subscriber.PublishAsync(
                RedisKey_AllGroup,
                JsonConvert.SerializeObject(message)
            ).ConfigureAwait(false);
        }

        public async Task Subscribe(string group, string connectionId)
        {
            if (!_groups.ContainsKey(group)) {
                var subscribers = _groups[group] = new ConcurrentDictionary<string, string>();

                var subscriber = _cache.Multiplexer.GetSubscriber();
                await subscriber.SubscribeAsync(
                    GetRedisKeyForGroup(group),
                    async (channel, message) => {
                        foreach (var connection in subscribers) {
                            await InvokeOnMessage(connection.Key, message);
                        }
                    }
                ).ConfigureAwait(false);
            }

            _groups[group].Add(connectionId, null);
        }

        public async Task Unsubscribe(string group, string connectionId)
        {
            if (_groups.ContainsKey(group)) {
                _groups[group].Remove(connectionId);

                if (_groups[group].Count == 0) {
                    var subscriber = _cache.Multiplexer.GetSubscriber();
                    await subscriber.UnsubscribeAsync(
                        GetRedisKeyForGroup(group)
                    ).ConfigureAwait(false);
                    _groups.Remove(group);
                }
            }
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

        public async Task SendMessageGroupAsync(string group, Message message)
        {
            var subscriber = _cache.Multiplexer.GetSubscriber();

            // Publish the message
            await subscriber.PublishAsync(
                GetRedisKeyForGroup(group),
                JsonConvert.SerializeObject(message)
            ).ConfigureAwait(false);
        }

        private async Task InvokeOnMessage(RedisChannel connectionId, RedisValue message) {
            if (OnMessage != null) {
                await OnMessage(
                    connectionId,
                    JsonConvert.DeserializeObject<Message>(message)
                ).ConfigureAwait(false);
            }
        }

        private static string GetRedisKeyForConnectionId(string connectionId) {
            return RedisKey_ConnectionPrefix + connectionId;
        }

        private static string GetRedisKeyForGroup(string group) {
            return RedisKey_GroupPrefix + group;
        }
    }
}
