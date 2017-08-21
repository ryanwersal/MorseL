using System.Net;
using System.Threading.Tasks;
using MorseL.Client.WebSockets.Tests;
using MorseL.Client;
using Xunit;
using MorseL.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using MorseL.Client.Middleware;
using MorseL.Client.WebSockets;
using MorseL.Extensions;
using MorseL.Sockets.Middleware;

namespace MorseL.Tests
{
    [Trait("Target", "EndToEndTests")]
    public class EndToEndTests
    {
        [Fact]
        public async void ConnectedCalledWhenClientConnectionEstablished()
        {
            using (new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000).Start())
            {
                var connectedCalled = false;
                var client = new Client.Connection("ws://localhost:5000/hub");
                client.Connected += () => connectedCalled = true;
                await client.StartAsync();
                Assert.True(connectedCalled);

                await client.DisposeAsync();
            }
        }

        [Fact]
        public async void ReconnectingDoesNotKillServer()
        {
            using (new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000).Start())
            {
                var connectedCalled = false;
                for (int i = 0; i < 10; i++)
                {
                    var client = new Client.Connection("ws://localhost:5000/hub");
                    client.Connected += () => connectedCalled = true;
                    await client.StartAsync();
                    var task = client.Invoke<object>("FooBar");

                    await Task.Delay(100);

                    await client.DisposeAsync();
                }
                Assert.True(connectedCalled);
            }
        }

        [Theory]
        [InlineData("NonExistentMethod", "SomeMethodArgument")]
        public async void CallToNonExistentHubMethodFromClientThrowsMissingHubMethodInClient(string methodName, params object[] arguments)
        {
            using (new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000).Start())
            {
                var client = new Connection("ws://localhost:5000/hub", null, o => o.ThrowOnMissingHubMethodInvoked = true);
                await client.StartAsync();

                var expectedMethodName = string.IsNullOrWhiteSpace(methodName) ? "[Invalid Method Name]" : methodName;
                var expectedArgumentList = arguments?.Length > 0 ? string.Join(", ", arguments) : "[No Parameters]";

                var exception = await Assert.ThrowsAsync<MorseLException>(() => client.Invoke<object>(methodName, arguments));
                Assert.Equal(
                    $"Cannot find method \"{expectedMethodName}({expectedArgumentList})\"",
                    exception.Message);

                await client.DisposeAsync();
            }
        }

        [Theory]
        [InlineData("", "SomeMethodArgument")]
        [InlineData(null, 5)]
        public async void CallToInvalidHubMethodFromClientThrowsMissingHubMethodInClient(string methodName, params object[] arguments)
        {
            using (new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000, (s, b) => {
                s.Configure<Extensions.MorseLOptions>(o => {
                    o.ThrowOnInvalidMessage = false;
                });
            }).Start())
            {
                var client = new Connection("ws://localhost:5000/hub", null, o => o.ThrowOnMissingHubMethodInvoked = true);
                await client.StartAsync();

                var expectedMethodName = string.IsNullOrWhiteSpace(methodName) ? "[Invalid Method Name]" : methodName;
                var expectedArgumentList = arguments?.Length > 0 ? string.Join(", ", arguments) : "[No Parameters]";

                var exception = await Assert.ThrowsAsync<MorseLException>(() => client.Invoke<object>(methodName, arguments));
                Assert.Equal(
                    $"Cannot find method \"{expectedMethodName}({expectedArgumentList})\"",
                    exception.Message);

                await client.DisposeAsync();
            }
        }

        [Theory]
        [InlineData("SomeNonExistentMethod", "SomeMethodArgument")]
        [InlineData("SomeOtherNonExistentMethod", 5)]
        public async void HubInvokingNonExistentClientMethodThrowsInClient(string methodName, params object[] arguments)
        {
            using (new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000, (s, b) => {
                s.Configure<Extensions.MorseLOptions>(o => {
                    o.ThrowOnInvalidMessage = false;
                });
            }).Start())
            {
                var client = new Connection("ws://localhost:5000/hub", null, o => o.ThrowOnMissingMethodRequest = true);
                await client.StartAsync();

                var expectedMethodName = string.IsNullOrWhiteSpace(methodName) ? "[Invalid Method Name]" : methodName;
                var expectedArgumentList = arguments?.Length > 0 ? string.Join(", ", arguments) : "[No Parameters]";

                Exception exception = null;
                client.Error += (exc) => exception = exc;

                await client.Invoke<int>(nameof(TestHub.CallInvalidClientMethod), methodName, arguments);

                await Task.Delay(100);

                Assert.NotNull(exception);
                Assert.Equal(
                    $"Invalid method request received; method is \"{expectedMethodName}({expectedArgumentList})\"",
                    exception.Message);

                await client.DisposeAsync();
            }
        }

        [Theory]
        [InlineData("SomeNonExistentMethod", "SomeMethodArgument")]
        [InlineData("SomeOtherNonExistentMethod", 5)]
        public async void HubInvokingNonExistentClientMethodThrowsInHub(string methodName, params object[] arguments)
        {
            Exception exception = null;
            using (new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000, (s, b) =>
                {
                    s.Configure<Extensions.MorseLOptions>(o => o.ThrowOnMissingClientMethodInvoked = true);
                },
                (app, services) =>
                {
                    app.Use(async (context, next) =>
                    {
                        try
                        {
                            await next.Invoke();
                        }
                        catch (Exception e)
                        {
                            exception = e;
                        }
                    });
                }).Start())
            {
                var client = new Connection("ws://localhost:5000/hub", null);
                await client.StartAsync();

                await client.Invoke<int>(nameof(TestHub.CallInvalidClientMethod), methodName, arguments);

                var expectedMethodName = string.IsNullOrWhiteSpace(methodName) ? "[Invalid Method Name]" : methodName;
                var expectedArgumentList = arguments?.Length > 0 ? string.Join(", ", arguments) : "[No Parameters]";

                await Task.Delay(100);

                Assert.NotNull(exception);
                Assert.Equal(
                    $"Error: Cannot find method \"{expectedMethodName}({expectedArgumentList})\" from {client.ConnectionId}",
                    exception.Message);

                await client.DisposeAsync();
            }
        }

        [Theory]
        [InlineData("SomeNonExistentMethod", "SomeMethodArgument")]
        [InlineData("SomeOtherNonExistentMethod", 5)]
        public async void HubInvokingNonExistentClientMethodThrowsInHubWithMiddleware(string methodName, params object[] arguments)
        {
            Exception exception = null;
            using (new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000, (s, b) =>
                {
                    s.Configure<Extensions.MorseLOptions>(o => o.ThrowOnMissingClientMethodInvoked = true);
                    b.AddMiddleware<Base64HubMiddleware>(ServiceLifetime.Transient);
                },
                (app, services) =>
                {
                    app.Use(async (context, next) =>
                    {
                        try
                        {
                            await next.Invoke();
                        }
                        catch (Exception e)
                        {
                            exception = e;
                        }
                    });
                }).Start())
            {
                var client = new Connection("ws://localhost:5000/hub");
                client.AddMiddleware(new Base64ClientMiddleware());

                await client.StartAsync();

                await client.Invoke<int>(nameof(TestHub.CallInvalidClientMethod), methodName, arguments);

                var expectedMethodName = string.IsNullOrWhiteSpace(methodName) ? "[Invalid Method Name]" : methodName;
                var expectedArgumentList = arguments?.Length > 0 ? string.Join(", ", arguments) : "[No Parameters]";

                await Task.Delay(100);

                Assert.NotNull(exception);
                Assert.Equal(
                    $"Error: Cannot find method \"{expectedMethodName}({expectedArgumentList})\" from {client.ConnectionId}",
                    exception.Message);

                await client.DisposeAsync();
            }
        }

        public class TestHub : Hub
        {
            public void FooBar() { }

            public async Task<int> CallInvalidClientMethod(string methodToCall, params object[] arguments)
            {
                // Call the method but don't block on it so our caller gets a response
                Clients.Client(Context.ConnectionId).InvokeAsync(methodToCall, arguments);
                return 5;
            }
        }

        public class Base64HubMiddleware : Sockets.Middleware.IMiddleware
        {
            public async Task SendAsync(ConnectionContext context, MiddlewareDelegate next)
            {
                await next(new ConnectionContext(
                    context.Connection,
                    new CryptoStream(context.Stream, new ToBase64Transform(), CryptoStreamMode.Read)));
            }

            public async Task ReceiveAsync(ConnectionContext context, MiddlewareDelegate next)
            {
                await next(new ConnectionContext(
                    context.Connection,
                    new CryptoStream(context.Stream, new FromBase64Transform(), CryptoStreamMode.Read)));
            }
        }

        public class Base64ClientMiddleware : Client.Middleware.IMiddleware
        {
            public async Task SendAsync(string data, TransmitDelegate next)
            {
                await next(Convert.ToBase64String(Encoding.UTF8.GetBytes(data)));
            }

            public async Task RecieveAsync(WebSocketPacket packet, RecieveDelegate next)
            {
                await next(new WebSocketPacket(packet.MessageType,
                    Convert.FromBase64String(Encoding.UTF8.GetString(packet.Data))));
            }
        }
    }
}
