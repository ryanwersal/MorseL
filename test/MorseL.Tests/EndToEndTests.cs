using System.Net;
using MorseL.Client;
using Xunit;
using MorseL.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using MorseL.Client.Middleware;
using MorseL.Client.WebSockets;
using MorseL.Extensions;
using MorseL.Shared.Tests;
using MorseL.Sockets.Middleware;
using Xunit.Abstractions;
using IClientMiddleware = MorseL.Client.Middleware.IMiddleware;
using ClientConnectionContext = MorseL.Client.Middleware.ConnectionContext;
using IHubMiddleware = MorseL.Sockets.Middleware.IMiddleware;
using HubConnectionContext = MorseL.Sockets.Middleware.ConnectionContext;
using System.IO;

namespace MorseL.Tests
{
    [Trait("Target", "EndToEndTests")]
    public class EndToEndTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public EndToEndTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async void ConnectedCalledWhenClientConnectionEstablished()
        {
            using (new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000).Start())
            {
                var connectedCalled = false;
                var client = new Connection("ws://localhost:5000/hub");
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
                    var client = new Connection("ws://localhost:5000/hub");
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

                await Task.Delay(5000);

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
        public async void HubInvokingNonExistentClientMethodThrowsInHubWithMiddleware(
            string methodName,
            params object[] arguments)
        {
            var tcs = new TaskCompletionSource<Exception>();
            string connectionId = null;
            
            using (new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000, (
                    s,
                    b) =>
                {
                    s.Configure<Extensions.MorseLOptions>(o => o.ThrowOnMissingClientMethodInvoked = true);
                    b.AddMiddleware<Base64HubMiddleware>(ServiceLifetime.Transient);
                },
                (
                    app,
                    services) =>
                {
                    app.Use(async (
                        context,
                        next) =>
                    {
                        try
                        {
                            await next.Invoke();
                        }
                        catch (Exception e)
                        {
                            tcs.SetResult(e);
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

                await Task.WhenAny(tcs.Task, Task.Delay(5000));

                Assert.True(tcs.Task.IsCompleted);

                var exception = await tcs.Task;
                Assert.NotNull(exception);
                Assert.Equal(
                    $"Error: Cannot find method \"{expectedMethodName}({expectedArgumentList})\" from {client.ConnectionId}",
                    exception.Message);

                await client.DisposeAsync();
            }
        }

        [Fact]
        public async void ClientThrowsUsefulExceptionWhenFailsToConnectNonExistentHost()
        {
            var client = new Connection("ws://localhost:5000/hub", null, o => o.ThrowOnMissingHubMethodInvoked = true);

            await Assert.ThrowsAnyAsync<SocketException>(() => client.StartAsync());

            await client.DisposeAsync();
        }

        [Fact]
        public async void ServerClosesDuringLongSendFromClientThrowsExceptionOnInvoker()
        {
            using (var server = new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000).Start())
            {
                var client = new Connection("ws://localhost:5000/hub", null,
                    o => o.ThrowOnMissingHubMethodInvoked = true);
                await client.StartAsync();

                var task = Task.Run(() => client.Invoke("LongRunningMethod"));

                // Disconnect the client
                server.Dispose();

                await Assert.ThrowsAnyAsync<WebSocketClosedException>(() => task);

                await client.DisposeAsync();
            }
        }

        [Fact]
        public async void ServerClosingConnectionDuringLongSendFromClientThrowsExceptionOnInvoker()
        {
            using (var server = new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000).Start())
            {
                var client = new Connection("ws://localhost:5000/hub", null,
                    o => o.ThrowOnMissingHubMethodInvoked = true);
                await client.StartAsync();

                var task = Task.Run(() => client.Invoke("LongRunningMethod"));

                // Disconnect the client
                await client.DisposeAsync();

                await Assert.ThrowsAnyAsync<MorseLException>(() => task);
            }
        }

        [Fact]
        public async void LongSendFromClientDoesNotBlockClientReceive()
        {
            using (new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000).Start())
            {
                var client = new Connection("ws://localhost:5000/hub", null, o => o.ThrowOnMissingHubMethodInvoked = true);
                await client.StartAsync();

                bool callbackFired = false;
                client.On("Callback", new Type[0], (args) =>
                {
                    callbackFired = true;
                });

                var hugeMessage = new StringBuilder("");
                for (var i = 0; i < 1000000; i++)
                {
                    hugeMessage.Append("abcdef");
                }

                await client.Invoke("PrimeCallback");
                await client.Invoke("SendHugeData", hugeMessage.ToString());

                Assert.True(callbackFired);

                await client.DisposeAsync();
            }
        }

        [Fact]
        public async void HubMethodInvokeDuringLongMethodResponseTimeDoesNotBlockInvocation()
        {
            using (new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000).Start())
            {
                var client = new Connection("ws://localhost:5000/hub", null, o => o.ThrowOnMissingHubMethodInvoked = true);
                await client.StartAsync();

                Task longRunningTask = null;
                bool callbackFired = false;
                client.On("Callback", new Type[0], async (args) =>
                {
                    await client.Invoke("DynamicCallback", "InResponseCallback");
                });
                client.On("InResponseCallback", () =>
                {
                    if (!longRunningTask?.IsCompleted == true)
                    {
                        callbackFired = true;
                    }
                });

                await client.Invoke("PrimeCallback");
                longRunningTask = client.Invoke("LongRunningMethod");
                await longRunningTask;

                Assert.True(callbackFired);

                await client.DisposeAsync();
            }
        }

        [Fact]
        public async void LongDelayUntilServerResponseDoesNotBlockClientCallbacks()
        {
            using (new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000).Start())
            {
                var client = new Connection("ws://localhost:5000/hub", null, o => o.ThrowOnMissingHubMethodInvoked = true);
                await client.StartAsync();

                Task longRunningTask = null;
                bool callbackFired = false;
                client.On("Callback", new Type[0], (args) =>
                {
                    if (!longRunningTask?.IsCompleted == true)
                    {
                        callbackFired = true;
                    }
                });

                await client.Invoke("PrimeCallback");
                longRunningTask = client.Invoke("LongRunningMethod");
                await longRunningTask;

                Assert.True(callbackFired);

                await client.DisposeAsync();
            }
        }

        [Theory]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(8)]
        [InlineData(16)]
        public async Task AsyncCallToMethodForResultGetsResultsAsExpected(int count)
        {
            using (new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000).Start())
            {
                var client = new Connection("ws://localhost:5000/hub");
                await client.StartAsync();

                var taskMap = new Dictionary<Task<int>, int>();
                var tasks = new List<Task<int>>();

                for (var i = 0; i < count; i++)
                {
                    var task = client.Invoke<int>("ExpectedResult", i);
                    taskMap[task] = i;
                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);

                foreach (var pair in taskMap)
                {
                    Assert.Equal(pair.Value, pair.Key.Result);
                }
            }
        }

        [Fact]
        public async Task HubThrowingExceptionDoesntCausePerpetualException()
        {
            using (new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000).Start())
            {
                var client = new Connection("ws://localhost:5000/hub");
                await client.StartAsync();

                try
                {
                    await client.Invoke(nameof(TestHub.ThrowException));
                }
                catch (Exception)
                {
                    // Ignore
                }

                client = new Connection("ws://localhost:5000/hub");
                await client.StartAsync();

                await client.Invoke("FooBar");
            }
        }

        [Fact]
        public async Task HubInvalidMethodExceptionDoesntCausePerpetualException()
        {
            using (new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000, (s, b) =>
            {
                s.Configure<Extensions.MorseLOptions>(o => o.ThrowOnInvalidMessage = false);
            }).Start())
            {
                var client = new Connection("ws://localhost:5000/hub", options: o =>
                {
                    o.ThrowOnInvalidMessage = true;
                    o.RethrowUnobservedExceptions = true;
                    o.ThrowOnMissingHubMethodInvoked = true;
                    o.ThrowOnMissingMethodRequest = true;
                });
                await client.StartAsync();

                try
                {
                    await client.Invoke(nameof(TestHub.ThrowInvalidMethodException), "InvalidArgument");
                }
                catch (Exception)
                {
                    // Ignore
                }

                client = new Connection("ws://localhost:5000/hub");
                await client.StartAsync();

                await client.Invoke("FooBar");
            }
        }

        [Fact]
        public async Task HubMissingMethodExceptionDoesntCausePerpetualException()
        {
            using (new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000, (s, b) =>
            {
                s.Configure<Extensions.MorseLOptions>(o => o.ThrowOnInvalidMessage = false);
            }).Start())
            {
                var client = new Connection("ws://localhost:5000/hub", options: o =>
                {
                    o.ThrowOnInvalidMessage = true;
                    o.RethrowUnobservedExceptions = true;
                    o.ThrowOnMissingHubMethodInvoked = true;
                    o.ThrowOnMissingMethodRequest = true;
                });
                await client.StartAsync();

                try
                {
                    await client.Invoke("SomeNonExistentMethod");
                }
                catch (Exception)
                {
                    // Ignore
                }

                client = new Connection("ws://localhost:5000/hub");
                await client.StartAsync();

                await client.Invoke("FooBar");
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(32)]
        [InlineData(64)]
        public async Task ParallelCallsToHubMethodsForResultDoesntDie(int count)
        {
            using (new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000, (s, b) =>
            {
                s.Configure<Extensions.MorseLOptions>(o => o.ThrowOnInvalidMessage = false);
            }).Start())
            {
                var client = new Connection("ws://localhost:5000/hub", options: o =>
                {
                    o.ThrowOnInvalidMessage = true;
                    o.RethrowUnobservedExceptions = true;
                    o.ThrowOnMissingHubMethodInvoked = true;
                    o.ThrowOnMissingMethodRequest = true;
                });
                await client.StartAsync();

                var tasks = new List<Task>();

                for (var i = 0; i < count; i++)
                {
                    tasks.Add(Task.Run(async () => await client.Invoke<int>(nameof(TestHub.ExpectedResult), i)));
                }

                await Task.WhenAll(tasks);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(32)]
        [InlineData(64)]
        public async Task ParallelCallsToHubMethodsAfterReconnectForResultDoesntDie(int count)
        {
            using (new SimpleMorseLServer<TestHub>(IPAddress.Any, 5000, (s, b) =>
            {
                s.Configure<Extensions.MorseLOptions>(o => o.ThrowOnInvalidMessage = false);
            }).Start())
            {
                var client = new Connection("ws://localhost:5000/hub", options: o =>
                {
                    o.ThrowOnInvalidMessage = true;
                    o.RethrowUnobservedExceptions = true;
                    o.ThrowOnMissingHubMethodInvoked = true;
                    o.ThrowOnMissingMethodRequest = true;
                });
                await client.StartAsync();

                var client2 = new Connection("ws://localhost:5000/hub", options: o =>
                {
                    o.ThrowOnInvalidMessage = true;
                    o.RethrowUnobservedExceptions = true;
                    o.ThrowOnMissingHubMethodInvoked = true;
                    o.ThrowOnMissingMethodRequest = true;
                });
                await client2.StartAsync();

                var tasks = new List<Task>();

                for (var i = 0; i < count; i++)
                {
                    var taskId = 10000 + i;
                    tasks.Add(client2.Invoke<int>(nameof(TestHub.ExpectedResult), taskId).ContinueWith(t =>
                    {
                        _testOutputHelper.WriteLine($"Completing task {t.Result}");
                    }));

                    taskId = i;
                    tasks.Add(client.Invoke<int>(nameof(TestHub.ExpectedResult), taskId).ContinueWith(t =>
                    {
                        _testOutputHelper.WriteLine($"Completing task {t.Result}");
                    }));
                }

                await Task.WhenAll(tasks);
            }
        }

        public class TestHub : Hub
        {
            public void FooBar() { }

            public Task PrimeCallback()
            {
                Task.Delay(50).ContinueWith((t) => Client.InvokeAsync("Callback"));
                return Task.CompletedTask;
            }

            public Task DynamicCallback(string callbackName)
            {
                Task.Delay(50).ContinueWith((t) => Client.InvokeAsync(callbackName));
                return Task.CompletedTask;
            }

            public Task<int> ExpectedResult(int result)
            {
                return Task.FromResult(result);
            }

            public async Task LongRunningMethod()
            {
                await Task.Delay(20000);
            }

            public async Task SendHugeData()
            {
                await Task.Delay(500);
            }

            public Task ThrowException()
            {
                throw new Exception("Derp");
            }

            public Task ThrowInvalidMethodException(int value)
            {
                throw new Exception("Derp");
            }

            public async Task<int> CallInvalidClientMethod(string methodToCall, params object[] arguments)
            {
                // Call the method but don't block on it so our caller gets a response
                Clients.Client(Context.ConnectionId).InvokeAsync(methodToCall, arguments);
                return 5;
            }
        }

        public class Base64HubMiddleware : IHubMiddleware
        {
            public async Task SendAsync(HubConnectionContext context, MiddlewareDelegate next)
            {
                using (var stream = new CryptoStream(context.Stream, new ToBase64Transform(), CryptoStreamMode.Read))
                {
                    await next(new HubConnectionContext(
                        context.Connection,
                        stream));
                }
            }

            public async Task ReceiveAsync(HubConnectionContext context, MiddlewareDelegate next)
            {
                using (var stream = new CryptoStream(context.Stream, new FromBase64Transform(), CryptoStreamMode.Read))
                {
                    await next(new HubConnectionContext(
                        context.Connection,
                        stream));
                }
            }
        }

        public class Base64ClientMiddleware : MorseL.Client.Middleware.IMiddleware
        {
            public async Task SendAsync(Stream stream, TransmitDelegate next)
            {
                using (var cryptoStream = new CryptoStream(stream, new ToBase64Transform(), CryptoStreamMode.Write))
                {
                    await next(cryptoStream);
                }
            }

            public async Task RecieveAsync(ClientConnectionContext context, RecieveDelegate next)
            {
                using (var cryptoStream = new CryptoStream(context.Stream, new FromBase64Transform(), CryptoStreamMode.Read))
                {
                    await next(new ClientConnectionContext(context.MessageType, cryptoStream));
                }
            }
        }
    }
}
