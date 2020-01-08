using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MorseL.Common;
using Newtonsoft.Json;
using StackExchange.Redis;

[assembly: InternalsVisibleTo("MorseL.Scaleout.Redis.Tests")]
namespace MorseL.Scaleout.Redis
{
    /// <summary>
    /// Redis backed backplane.
    /// 
    /// Group subscriptions work by treating the RedisBackplane as the subscriber for groups.
    /// The RedisBackplane keeps its own list of groups and subscribers so when a message
    /// comes in for a group to the RedisBackplane, the RedisBackplane can handle sending
    /// out the messages itself to the internal clients. Redis allows for multiple handlers
    /// per subscription but either way some state will have to be tracked.
    /// 
    /// It might turn out to be more useful to leverage Redis' implementation but this works
    /// for now.
    /// </summary>
    public class RedisBackplane : IBackplane
    {
        private const string RedisKeyAllGroup = "redis-backplane:group:all";
        private const string RedisKeyConnectionPrefix = "redis-backplane:con:";
        private const string RedisKeyGroupPrefix = "redis-backplane:group:";

        private readonly Lazy<ConnectionMultiplexer> _lazyConnection;
        private ConnectionMultiplexer Connection => _lazyConnection.Value;
        private IDatabase Cache => Connection.GetDatabase();

        /// <summary>
        /// Keeps track of connected Connection IDs. 
        /// Concurrent HashSet (ConnectionId -> Nothing)
        /// </summary>
        private readonly IDictionary<string, OnMessageDelegate> _connections = new ConcurrentDictionary<string, OnMessageDelegate>();

        /// <summary>
        /// Keeps track of locally registered groups to Connection IDs.
        /// Outer dictionary is (Group -> HashSet of (ConnectionId -> Nothing))
        /// </summary>
        private readonly IDictionary<string, IDictionary<string, object>> _groups = new ConcurrentDictionary<string, IDictionary<string, object>>();

        /// <summary>
        /// Reverse dictionary of Connection IDs to set of registered groups.
        /// Outer dictionary is (ConnectionId -> HashSet of (Group names -> Nothing))
        /// </summary>
        private readonly IDictionary<string, IDictionary<string, object>> _subscriptions = new ConcurrentDictionary<string, IDictionary<string, object>>();

        internal int OnMessageCount => _connections.Count();

        public RedisBackplane(IOptions<ConfigurationOptions> options)
        {
            _lazyConnection = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(options.Value));

            // TODO : Offload this from the constructor
            var subscriber = Cache.Multiplexer.GetSubscriber();
            subscriber.Subscribe(
                RedisKeyAllGroup,
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

        public async Task OnClientConnectedAsync(string connectionId, OnMessageDelegate onMessageDelegate)
        {
            _connections.Add(connectionId, onMessageDelegate);
            var subscriber = Cache.Multiplexer.GetSubscriber();

            // Subscribe to messages for the client
            await subscriber.SubscribeAsync(
                GetRedisKeyForConnectionId(connectionId),
                async (channel, message) => await InvokeOnMessage(connectionId, message)
            ).ConfigureAwait(false);
        }

        public async Task OnClientDisconnectedAsync(string connectionId)
        {
            _connections.Remove(connectionId);
            var subscriber = Cache.Multiplexer.GetSubscriber();

            // Unsubscribe for messages to the client
            await subscriber.UnsubscribeAsync(
                GetRedisKeyForConnectionId(connectionId)
            ).ConfigureAwait(false);

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

        public async Task SendMessageAsync(string connectionId, Message message)
        {
            var subscriber = Cache.Multiplexer.GetSubscriber();

            // Publish the message
            await subscriber.PublishAsync(
                GetRedisKeyForConnectionId(connectionId),
                JsonConvert.SerializeObject(message)
            ).ConfigureAwait(false);
        }

        public async Task SendMessageAllAsync(Message message)
        {
            var subscriber = Cache.Multiplexer.GetSubscriber();

            // Publish the message
            await subscriber.PublishAsync(
                RedisKeyAllGroup,
                JsonConvert.SerializeObject(message)
            ).ConfigureAwait(false);
        }

        public async Task Subscribe(string group, string connectionId)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            if (connectionId == null) throw new ArgumentNullException(nameof(connectionId));

            if (!_groups.ContainsKey(group)) {
                var subscribers = _groups[group] = new ConcurrentDictionary<string, object>();

                var subscriber = Cache.Multiplexer.GetSubscriber();
                await subscriber.SubscribeAsync(
                    GetRedisKeyForGroup(group),
                    async (channel, message) => {
                        foreach (var connection in subscribers) {
                            await InvokeOnMessage(connection.Key, message);
                        }
                    }
                ).ConfigureAwait(false);
            }

            if (!_subscriptions.ContainsKey(connectionId))
            {
                _subscriptions[connectionId] = new ConcurrentDictionary<string, object>();
            }

            _groups[group].Add(connectionId, null);
            _subscriptions[connectionId].Add(group, null);
        }

        public async Task Unsubscribe(string group, string connectionId)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            if (connectionId == null) throw new ArgumentNullException(nameof(connectionId));

            if (_groups.ContainsKey(group)) {
                _groups[group].Remove(connectionId);

                // If no one is subscribed, remove the global group subscription
                if (_groups[group].Count == 0) {
                    var subscriber = Cache.Multiplexer.GetSubscriber();
                    await subscriber.UnsubscribeAsync(
                        GetRedisKeyForGroup(group)
                    ).ConfigureAwait(false);

                    // Remove the group instance
                    _groups.Remove(group);
                }
            }

            if (_subscriptions.ContainsKey(connectionId))
            {
                _subscriptions[connectionId].Remove(group);

                // Delete the subscription container if we don't need it
                if (_subscriptions[connectionId].Count == 0)
                {
                    _subscriptions.Remove(connectionId);
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
            var subscriber = Cache.Multiplexer.GetSubscriber();

            // Publish the message
            await subscriber.PublishAsync(
                GetRedisKeyForGroup(group),
                JsonConvert.SerializeObject(message)
            ).ConfigureAwait(false);
        }

        private async Task InvokeOnMessage(RedisChannel connectionId, RedisValue message) {
            if (_connections.TryGetValue(connectionId, out var messageDelegate) && messageDelegate != null)
            {
                await messageDelegate.Invoke(
                    connectionId,
                    JsonConvert.DeserializeObject<Message>(message)
                ).ConfigureAwait(false);
            }
        }

        private static string GetRedisKeyForConnectionId(string connectionId) {
            return RedisKeyConnectionPrefix + connectionId;
        }

        private static string GetRedisKeyForGroup(string group) {
            return RedisKeyGroupPrefix + group;
        }

        internal IDictionary<string, OnMessageDelegate> Connections => _connections;
        internal IDictionary<string, IDictionary<string, object>> Groups => _groups;
        internal IDictionary<string, IDictionary<string, object>> Subscriptions => _subscriptions;
    }
}
