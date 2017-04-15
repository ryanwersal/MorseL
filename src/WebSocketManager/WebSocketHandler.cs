using System;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using WebSocketManager.Common;

namespace WebSocketManager
{
    public abstract class WebSocketHandler
    {
        protected WebSocketConnectionManager WebSocketConnectionManager { get; set; }
        private JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        protected WebSocketHandler(WebSocketConnectionManager webSocketConnectionManager)
        {
            WebSocketConnectionManager = webSocketConnectionManager;
        }

        public virtual async Task OnConnected(WebSocket socket)
        {
            WebSocketConnectionManager.AddSocket(socket);

            await socket.SendMessageAsync(new Message()
            {
                MessageType = MessageType.ConnectionEvent,
                Data = WebSocketConnectionManager.GetId(socket)
            }).ConfigureAwait(false);
        }

        public virtual async Task OnDisconnected(WebSocket socket)
        {
            await WebSocketConnectionManager.RemoveSocket(WebSocketConnectionManager.GetId(socket)).ConfigureAwait(false);
        }

        public async Task SendMessageToAllAsync(Message message)
        {
            var openSockets = WebSocketConnectionManager.GetAll()
                .Where(s => s.State == WebSocketState.Open)
                .ToList();
            foreach (var socket in openSockets)
            {
                await socket.SendMessageAsync(message).ConfigureAwait(false);
            }
        }

        public async Task InvokeClientMethodToAllAsync(string methodName, params object[] arguments)
        {
            var openSockets = WebSocketConnectionManager.GetAll()
                .Where(s => s.State == WebSocketState.Open)
                .ToList();
            foreach (var socket in openSockets)
            {
                await socket.InvokeClientMethodAsync(methodName, arguments).ConfigureAwait(false);
            }
        }

        public async Task ReceiveAsync(WebSocket socket, WebSocketReceiveResult result, string serializedInvocationDescriptor)
        {
            var invocationDescriptor = JsonConvert.DeserializeObject<InvocationDescriptor>(serializedInvocationDescriptor);

            var method = this.GetType().GetMethod(invocationDescriptor.MethodName);

            if (method == null)
            {
                await socket.SendMessageAsync(new Message()
                {
                    MessageType = MessageType.Text,
                    Data = $"Cannot find method {invocationDescriptor.MethodName}"
                }).ConfigureAwait(false);
                return;
            }

            var invocationResultDescriptor = new InvocationResultDescriptor {Id = invocationDescriptor.Id};

            try
            {
                dynamic methodResult = method.Invoke(this, invocationDescriptor.Arguments);
                invocationResultDescriptor.Result = await methodResult;
            }

            catch (TargetParameterCountException)
            {
                await socket.SendMessageAsync(new Message()
                    {
                        MessageType = MessageType.Text,
                        Data =
                            $"The {invocationDescriptor.MethodName} method does not take {invocationDescriptor.Arguments.Length} parameters!"
                    })
                    .ConfigureAwait(false);
            }

            catch (ArgumentException)
            {
                await socket.SendMessageAsync(new Message()
                    {
                        MessageType = MessageType.Text,
                        Data = $"The {invocationDescriptor.MethodName} method takes different arguments!"
                    })
                    .ConfigureAwait(false);
            }

            catch (Exception e)
            {
                invocationResultDescriptor.Error = e.ToString();
            }

            await socket.SendMessageAsync(new Message
            {
                MessageType = MessageType.InvocationResult,
                Data = JsonConvert.SerializeObject(invocationResultDescriptor)
            });
        }
    }
}