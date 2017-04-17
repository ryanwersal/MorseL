using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Reflection;
using WebSocketManager.Common;
using WebSocketManager.Common.Serialization;

namespace WebSocketManager
{
    public abstract class WebSocketHandler
    {
        protected WebSocketConnectionManager WebSocketConnectionManager { get; set; }

        public MethodInfo[] HandlerMethods { get; }

        protected WebSocketHandler(WebSocketConnectionManager webSocketConnectionManager)
        {
            WebSocketConnectionManager = webSocketConnectionManager;

            HandlerMethods = this.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        }

        public virtual async Task OnConnected(WebSocket socket)
        {
            var connection = WebSocketConnectionManager.AddSocket(socket);

            await connection.Socket.SendMessageAsync(new Message()
            {
                MessageType = MessageType.ConnectionEvent,
                Data = connection.Id
            }).ConfigureAwait(false);

            await OnConnected(connection);
        }

        public virtual async Task OnConnected(Connection connection)
        {
            await Task.CompletedTask;
        }

        public virtual async Task OnDisconnected(WebSocket socket)
        {
            var connection = WebSocketConnectionManager.GetConnection(socket);

            await WebSocketConnectionManager.RemoveConnection(connection.Id).ConfigureAwait(false);

            await OnDisconnected(connection);
        }

        public virtual async Task OnDisconnected(Connection connection)
        {
            await Task.CompletedTask;
        }

        public async Task SendMessageToAllAsync(Message message)
        {
            var openConnections = WebSocketConnectionManager.GetAll()
                .Where(s => s.Socket.State == WebSocketState.Open)
                .ToList();
            foreach (var connection in openConnections)
            {
                await connection.Socket.SendMessageAsync(message).ConfigureAwait(false);
            }
        }

        public async Task InvokeClientMethodToAllAsync(string methodName, params object[] arguments)
        {
            var openConnections = WebSocketConnectionManager.GetAll()
                .Where(c => c.Socket.State == WebSocketState.Open)
                .ToList();
            foreach (var connection in openConnections)
            {
                await connection.Socket.InvokeClientMethodAsync(methodName, arguments).ConfigureAwait(false);
            }
        }

        public async Task ReceiveAsync(WebSocket socket, WebSocketReceiveResult result, string serializedInvocationDescriptor)
        {
            var invocationDescriptor = Json.DeserializeInvocationDescriptor(serializedInvocationDescriptor, HandlerMethods);

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

                var returnType = method.ReturnType;
                var hasGetAwaiter = returnType.GetMethod("GetAwaiter") != null;
                if (hasGetAwaiter)
                {
                    invocationResultDescriptor.Result = await methodResult.ConfigureAwait(false);
                }
                else
                {
                    invocationResultDescriptor.Result = methodResult;
                }
            }

            catch (TargetParameterCountException e)
            {
                await socket.SendMessageAsync(new Message()
                    {
                        MessageType = MessageType.Text,
                        Data =
                            $"The {invocationDescriptor.MethodName} method does not take {invocationDescriptor.Arguments.Length} parameters!"
                    })
                    .ConfigureAwait(false);
            }

            catch (ArgumentException e)
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
                Data = Json.SerializeObject(invocationResultDescriptor)
            });
        }
    }
}