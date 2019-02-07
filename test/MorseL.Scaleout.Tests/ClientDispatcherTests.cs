using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MorseL.Common;
using MorseL.Common.Serialization;
using MorseL.Extensions;
using MorseL.Scaleout;
using Xunit;

namespace MorseL.Scaleout.Tests
{
    [Trait("Category", "Scaleout")]
    public class ClientDispatcherTests
    {
        [Theory]
        [InlineData(MessageType.Text, "Some message")]
        [InlineData(MessageType.Text, "Some other message")]
        public async void SendMessageAllInvokesBackplaneSendMessageAll(MessageType type, string message)
        {
            var backplane = new TestBackplane();
            var serviceProvider = CreateServiceProvider(o => {
                o.AddSingleton<IBackplane>(_ => backplane);
            });
            var clientDispatcher = ActivatorUtilities.CreateInstance<ClientsDispatcher>(serviceProvider);

            var exception = await Assert.ThrowsAnyAsync<NotImplementedException>(() => clientDispatcher.All.SendMessageAsync(new Message {
                MessageType = type,
                Data = message
            }));
            Assert.Equal(message, exception.Message);
        }

        [Theory]
        [InlineData("methodName", "some argument")]
        [InlineData("anotherMethodName", "some argument", "another argument")]
        public async void InvokeMethodAllInvokesBackplaneSendMessageAll(string methodName, params object[] arguments)
        {
            var backplane = new TestBackplane();
            var serviceProvider = CreateServiceProvider(o => {
                o.AddSingleton<IBackplane>(_ => backplane);
            });
            var clientDispatcher = ActivatorUtilities.CreateInstance<ClientsDispatcher>(serviceProvider);

            var exception = await Assert.ThrowsAnyAsync<NotImplementedException>(
                () => clientDispatcher.All.InvokeAsync(methodName, arguments));
            Assert.Equal(MessageSerializer.SerializeObject<InvocationDescriptor>(new InvocationDescriptor {
                MethodName = methodName,
                Arguments = arguments
            }), exception.Message);
        }

        [Theory]
        [InlineData("connection-id", MessageType.Text, "Some message")]
        public async void SendMessageInvokesBackplaneSendMessage(string connectionId, MessageType type, string message)
        {
            var backplane = new TestBackplane();
            var serviceProvider = CreateServiceProvider(o => {
                o.AddSingleton<IBackplane>(_ => backplane);
            });
            var clientDispatcher = ActivatorUtilities.CreateInstance<ClientsDispatcher>(serviceProvider);

            var exception = await Assert.ThrowsAnyAsync<NotImplementedException>(
                () => clientDispatcher.Client(connectionId).SendMessageAsync(new Message {
                    MessageType = type,
                    Data = message
                }));
            Assert.Equal(connectionId+message, exception.Message);
        }

        [Theory]
        [InlineData("connection-id", "methodName", "some argument")]
        public async void InvokeMethodInvokesBackplaneSendMessage(string connectionId, string methodName, params object[] arguments)
        {
            var backplane = new TestBackplane();
            var serviceProvider = CreateServiceProvider(o => {
                o.AddSingleton<IBackplane>(_ => backplane);
            });
            var clientDispatcher = ActivatorUtilities.CreateInstance<ClientsDispatcher>(serviceProvider);

            var exception = await Assert.ThrowsAnyAsync<NotImplementedException>(
                () => clientDispatcher.Client(connectionId).InvokeAsync(methodName, arguments));
            Assert.Equal(connectionId + MessageSerializer.SerializeObject<InvocationDescriptor>(new InvocationDescriptor {
                MethodName = methodName,
                Arguments = arguments
            }), exception.Message);
        }

        private IServiceProvider CreateServiceProvider(Action<ServiceCollection> addServices = null)
        {
            var services = new ServiceCollection();
            services.AddOptions()
                .AddLogging()
                .AddMorseL();

            addServices?.Invoke(services);

            return services.BuildServiceProvider();
        }
    }

    public class TestBackplane : IBackplane
    {
        public Func<string, Task> OnClientConnectedCallback { get; set; } =
            (id) => throw new NotImplementedException(nameof(OnClientConnectedAsync));
        public Func<string, Task> OnClientDisconnectedCallback { get; set; } =
            (id) => throw new NotImplementedException(nameof(OnClientDisconnectedAsync));

        public Task OnClientConnectedAsync(string connectionId, OnMessageDelegate onMessageDelegate)
        {
            return OnClientConnectedCallback(connectionId);
        }

        public Task OnClientDisconnectedAsync(string connectionId)
        {
            return OnClientDisconnectedCallback(connectionId);
        }

        public Task DisconnectClientAsync(string connectionId)
        {
            throw new NotImplementedException(connectionId);
        }

        public Task SendMessageAllAsync(Message message)
        {
            throw new NotImplementedException(message.Data);
        }

        public Task SendMessageAsync(string connectionId, Message message)
        {
            throw new NotImplementedException(connectionId + message.Data);
        }

        public Task SendMessageGroupAsync(string group, Message message)
        {
            throw new NotImplementedException();
        }

        public Task Subscribe(string group, string connectionId)
        {
            throw new NotImplementedException();
        }

        public Task SubscribeAll(string group)
        {
            throw new NotImplementedException();
        }

        public Task Unsubscribe(string group, string connectionId)
        {
            throw new NotImplementedException();
        }

        public Task UnsubscribeAll(string group)
        {
            throw new NotImplementedException();
        }
    }
}
