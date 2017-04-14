using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using WebSocketManager.Common;

namespace WebSocketManager.Client
{
    public class Connection
    {
        public string ConnectionId { get; set; }

        private ClientWebSocket _clientWebSocket { get; set; }
        private JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
        private IDictionary<string, InvocationHandler> _handlers = new Dictionary<string, InvocationHandler>();

        public Connection()
        {
            _clientWebSocket = new ClientWebSocket();
        }

        public async Task StartConnectionAsync(string uri)
        {
            await _clientWebSocket.ConnectAsync(new Uri(uri), CancellationToken.None).ConfigureAwait(false);

            await Receive(message =>
            {
                switch (message.MessageType)
                {
                    case MessageType.ConnectionEvent:
                        ConnectionId = message.Data;
                        break;

                    case MessageType.ClientMethodInvocation:
                        var invocationDescriptor = JsonConvert.DeserializeObject<InvocationDescriptor>(message.Data, _jsonSerializerSettings);
                        InvokeOn(invocationDescriptor);
                        break;
                }
            });
        }

        public void On(string methodName, Action<object[]> handler)
        {
            var invocationHandler = new InvocationHandler(handler, new Type[] { });
            _handlers.Add(methodName, invocationHandler);
        }

        public async Task Invoke(string methodName, params object[] args)
        {
            var invocationDescriptor = new InvocationDescriptor
            {
                MethodName = methodName,
                Arguments = args
            };
            var message = JsonConvert.SerializeObject(invocationDescriptor, _jsonSerializerSettings);
            await _clientWebSocket.SendAllAsync(message, CancellationToken.None).ConfigureAwait(false);
        }

        private void InvokeOn(InvocationDescriptor invocationDescriptor)
        {
            var invocationHandler = _handlers[invocationDescriptor.MethodName];
            invocationHandler?.Handler(invocationDescriptor.Arguments);
        }

        public async Task StopConnectionAsync()
        {
            await _clientWebSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).ConfigureAwait(false);
        }

        private async Task Receive(Action<Message> handleMessage)
        {
            while (_clientWebSocket.State == WebSocketState.Open)
            {
                using (var receivedMessage = await _clientWebSocket.ReceiveAllAsync(CancellationToken.None)
                    .ConfigureAwait(false))
                {
                    switch (receivedMessage.MessageType)
                    {
                        case WebSocketMessageType.Binary:
                            // TODO: Implement.
                            throw new NotImplementedException("Binary messages not supported.");
                            break;

                        case WebSocketMessageType.Text:
                            var serializedMessage = await receivedMessage.ToStringAsync().ConfigureAwait(false);
                            var message = JsonConvert.DeserializeObject<Message>(serializedMessage);
                            handleMessage(message);
                            break;

                        case WebSocketMessageType.Close:
                            await _clientWebSocket
                                .CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None)
                                .ConfigureAwait(false);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
        }
    }
}