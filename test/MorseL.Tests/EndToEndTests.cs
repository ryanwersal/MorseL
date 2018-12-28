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
using MorseL.Shared.Tests;
using MorseL.Sockets.Middleware;
using Xunit.Abstractions;
using IClientMiddleware = MorseL.Client.Middleware.IMiddleware;
using ClientConnectionContext = MorseL.Client.Middleware.ConnectionContext;
using IHubMiddleware = MorseL.Sockets.Middleware.IMiddleware;
using HubConnectionContext = MorseL.Sockets.Middleware.ConnectionContext;
using MorseL.Common.WebSockets.Exceptions;
using MorseL.Extensions;
using MorseL.Diagnostics;
using System.Net.WebSockets;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace MorseL.Tests
{
    public class Context
    {
        public readonly PortPool PortPool = new PortPool(5050, 5100);
    }

    [Trait("Target", "EndToEndTests")]
    public class EndToEndTests : IClassFixture<Context>
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private ILogger _logger;

        private Context _context;

        public EndToEndTests(Context context, ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            _logger = new TestOutputHelperLogger(_testOutputHelper);
            _context = context;
        }

        [Fact]
        public async void ConnectedCalledWhenClientConnectionEstablished()
        {
            using (var server = new SimpleMorseLServer<TestHub>(logger: _logger))
            {
                await server.Start(_context.PortPool);

                var tcs = new TaskCompletionSource<object>();
                var client = new Connection(server.Uri, logger: _logger);
                client.Connected += () => tcs.TrySetResult(null);
                await client.StartAsync();

                await Task.WhenAny(tcs.Task, Task.Delay(5000));

                Assert.True(tcs.Task.IsCompletedSuccessfully);

                await client.DisposeAsync();
            }
        }

        [Fact]
        public async void ReconnectingDoesNotKillServer()
        {
            using (var server = new SimpleMorseLServer<TestHub>(logger: _logger))
            {
                await server.Start(_context.PortPool);

                var connectedCalled = false;
                for (int i = 0; i < 10; i++)
                {
                    var client = new Connection(server.Uri, logger: _logger);
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
            using (var server = new SimpleMorseLServer<TestHub>(logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Connection(server.Uri, null, o => o.ThrowOnMissingHubMethodInvoked = true, logger: _logger);
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
            using (var server = new SimpleMorseLServer<TestHub>((s, b) =>
            {
                s.Configure<Extensions.MorseLOptions>(o =>
                {
                    o.ThrowOnInvalidMessage = false;
                });
            }, logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Connection(server.Uri, null, o => o.ThrowOnMissingHubMethodInvoked = true, logger: _logger);
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
            using (var server = new SimpleMorseLServer<TestHub>((s, b) =>
            {
                s.Configure<Extensions.MorseLOptions>(o =>
                {
                    o.ThrowOnInvalidMessage = false;
                });
            }, logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Connection(server.Uri, null, o => o.ThrowOnMissingMethodRequest = true, logger: _logger);
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
            var tcs = new TaskCompletionSource<Exception>();

            using (var server = new SimpleMorseLServer<TestHub>((s, b) =>
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
                            tcs.SetResult(e);
                        }
                    });
                }, logger: _logger))
            {

                await server.Start(_context.PortPool);

                var client = new Connection(server.Uri, null);
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

        [Theory]
        [InlineData("SomeNonExistentMethod", "SomeMethodArgument")]
        [InlineData("SomeOtherNonExistentMethod", 5)]
        public async void HubInvokingNonExistentClientMethodThrowsInHubWithMiddleware(string methodName, params object[] arguments)
        {
            var tcs = new TaskCompletionSource<Exception>();

            using (var server = new SimpleMorseLServer<TestHub>((s, b) =>
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
                        tcs.SetResult(e);
                    }
                });
            }, logger: _logger))
            {

                await server.Start(_context.PortPool);

                var client = new Connection(
                    server.Uri,
                    options: options => options.ConnectionTimeout = TimeSpan.FromSeconds(5),
                    logger: new TestOutputHelperLogger(_testOutputHelper));
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
            var client = new Connection("ws://localhost:5200/hub", null, o => o.ThrowOnMissingHubMethodInvoked = true, logger: _logger);

            await Assert.ThrowsAnyAsync<WebSocketException>(() => client.StartAsync());

            await client.DisposeAsync();
        }

        [Fact]
        public async void ServerClosesDuringLongSendFromClientThrowsExceptionOnInvoker()
        {
            using (var server = new SimpleMorseLServer<TestHub>(logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Connection(server.Uri, null, o => o.ThrowOnMissingHubMethodInvoked = true, logger: _logger);
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
            using (var server = new SimpleMorseLServer<TestHub>(logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Connection(server.Uri, null, o => o.ThrowOnMissingHubMethodInvoked = true, logger: _logger);
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
            using (var server = new SimpleMorseLServer<TestHub>(logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Connection(server.Uri, null, o => o.ThrowOnMissingHubMethodInvoked = true, logger: _logger);
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
            using (var server = new SimpleMorseLServer<TestHub>(logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Connection(server.Uri, null, o => o.ThrowOnMissingHubMethodInvoked = true, logger: _logger);
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
            using (var server = new SimpleMorseLServer<TestHub>(logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Connection(server.Uri, null, o => o.ThrowOnMissingHubMethodInvoked = true, logger: _logger);
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
            using (var server = new SimpleMorseLServer<TestHub>(logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Connection(server.Uri, logger: _logger);
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

                await client.DisposeAsync();
            }
        }

        [Fact]
        public async Task HubThrowingExceptionDoesntCausePerpetualException()
        {
            using (var server = new SimpleMorseLServer<TestHub>(logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Connection(server.Uri, logger: _logger);
                await client.StartAsync();

                try
                {
                    await client.Invoke(nameof(TestHub.ThrowException));
                }
                catch (Exception)
                {
                    // Ignore
                }

                client = new Connection(server.Uri);
                await client.StartAsync();

                await client.Invoke("FooBar");

                await client.DisposeAsync();
            }
        }

        [Fact]
        public async Task HubInvalidMethodExceptionDoesntCausePerpetualException()
        {
            using (var server = new SimpleMorseLServer<TestHub>((s, b) =>
            {
                s.Configure<Extensions.MorseLOptions>(o => o.ThrowOnInvalidMessage = false);
            }, logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Connection(server.Uri, options: o =>
                {
                    o.ThrowOnInvalidMessage = true;
                    o.RethrowUnobservedExceptions = true;
                    o.ThrowOnMissingHubMethodInvoked = true;
                    o.ThrowOnMissingMethodRequest = true;
                }, logger: _logger);
                await client.StartAsync();

                try
                {
                    await client.Invoke(nameof(TestHub.ThrowInvalidMethodException), "InvalidArgument");
                }
                catch (Exception)
                {
                    // Ignore
                }

                client = new Connection(server.Uri);
                await client.StartAsync();

                await client.Invoke("FooBar");

                await client.DisposeAsync();
            }
        }

        [Fact]
        public async Task HubMissingMethodExceptionDoesntCausePerpetualException()
        {
            using (var server = new SimpleMorseLServer<TestHub>((s, b) =>
            {
                s.Configure<Extensions.MorseLOptions>(o => o.ThrowOnInvalidMessage = false);
            }, logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Connection(server.Uri, options: o =>
                {
                    o.ThrowOnInvalidMessage = true;
                    o.RethrowUnobservedExceptions = true;
                    o.ThrowOnMissingHubMethodInvoked = true;
                    o.ThrowOnMissingMethodRequest = true;
                }, logger: _logger);
                await client.StartAsync();

                try
                {
                    await client.Invoke("SomeNonExistentMethod");
                }
                catch (Exception)
                {
                    // Ignore
                }

                client = new Connection(server.Uri);
                await client.StartAsync();

                await client.Invoke("FooBar");

                await client.DisposeAsync();
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
            using (var server = new SimpleMorseLServer<TestHub>((s, b) =>
            {
                s.Configure<Extensions.MorseLOptions>(o => o.ThrowOnInvalidMessage = false);
            }, logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Connection(server.Uri, options: o =>
                {
                    o.ThrowOnInvalidMessage = true;
                    o.RethrowUnobservedExceptions = true;
                    o.ThrowOnMissingHubMethodInvoked = true;
                    o.ThrowOnMissingMethodRequest = true;
                }, logger: _logger);
                await client.StartAsync();

                var tasks = new List<Task>();

                for (var i = 0; i < count; i++)
                {
                    tasks.Add(client.Invoke<int>(nameof(TestHub.ExpectedResult), i));
                }

                await Task.WhenAll(tasks);

                await client.DisposeAsync();
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
            using (var server = new SimpleMorseLServer<TestHub>((s, b) =>
            {
                s.Configure<Extensions.MorseLOptions>(o => o.ThrowOnInvalidMessage = false);
            }, logger: _logger))
            {
                await server.Start(_context.PortPool);

                var client = new Connection(server.Uri, options: o =>
                {
                    o.ThrowOnInvalidMessage = true;
                    o.RethrowUnobservedExceptions = true;
                    o.ThrowOnMissingHubMethodInvoked = true;
                    o.ThrowOnMissingMethodRequest = true;
                }, logger: _logger);
                await client.StartAsync();

                var client2 = new Connection(server.Uri, options: o =>
                {
                    o.ThrowOnInvalidMessage = true;
                    o.RethrowUnobservedExceptions = true;
                    o.ThrowOnMissingHubMethodInvoked = true;
                    o.ThrowOnMissingMethodRequest = true;
                }, logger: _logger);
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

                await client.DisposeAsync();
                await client2.DisposeAsync();
            }
        }

        [Fact(Skip = "Manual run only")]
        public async Task SendHugePayload()
        {
            // Generate a large payload
            using (var server = new SimpleMorseLServer<TestHub>())
            {
                await server.Start(_context.PortPool);

                var client = new Connection(server.Uri);
                await client.StartAsync();

                var taskMap = new Dictionary<Task<int>, int>();
                var tasks = new List<Task<int>>();

                while (true)
                {
                    await client.Invoke(nameof(TestHub.SendHugePayload), (object) new object[10].Select(x => new Payload(20000)).ToArray());
                }
            }
        }

        public class Payload
        {
            public string A { get; set; }
            public string B { get; set; }
            public string C { get; set; }
            public string D { get; set; }
            public string E { get; set; }
            public string F { get; set; }
            public string G { get; set; }
            public string H { get; set; }
            public string I { get; set; }
            public string J { get; set; }
            public string K { get; set; }
            public string L { get; set; }
            public string M { get; set; }
            public string N { get; set; }
            public string O { get; set; }
            public string P { get; set; }
            public string Q { get; set; }
            public string R { get; set; }
            public string S { get; set; }
            public string T { get; set; }

            public Payload(int bucketSize)
            {
                var random = new Random();
                A = new object[(bucketSize / 32) + 1].Select(x => Guid.NewGuid().ToString("N")).Aggregate("", (x, y) => x + y.ToString()).Substring(0, bucketSize);
                B = new object[(bucketSize / 32) + 1].Select(x => Guid.NewGuid().ToString("N")).Aggregate("", (x, y) => x + y.ToString()).Substring(0, bucketSize);
                C = new object[(bucketSize / 32) + 1].Select(x => Guid.NewGuid().ToString("N")).Aggregate("", (x, y) => x + y.ToString()).Substring(0, bucketSize);
                D = new object[(bucketSize / 32) + 1].Select(x => Guid.NewGuid().ToString("N")).Aggregate("", (x, y) => x + y.ToString()).Substring(0, bucketSize);
                E = new object[(bucketSize / 32) + 1].Select(x => Guid.NewGuid().ToString("N")).Aggregate("", (x, y) => x + y.ToString()).Substring(0, bucketSize);
                F = new object[(bucketSize / 32) + 1].Select(x => Guid.NewGuid().ToString("N")).Aggregate("", (x, y) => x + y.ToString()).Substring(0, bucketSize);
                G = new object[(bucketSize / 32) + 1].Select(x => Guid.NewGuid().ToString("N")).Aggregate("", (x, y) => x + y.ToString()).Substring(0, bucketSize);
                H = new object[(bucketSize / 32) + 1].Select(x => Guid.NewGuid().ToString("N")).Aggregate("", (x, y) => x + y.ToString()).Substring(0, bucketSize);
                I = new object[(bucketSize / 32) + 1].Select(x => Guid.NewGuid().ToString("N")).Aggregate("", (x, y) => x + y.ToString()).Substring(0, bucketSize);
                J = new object[(bucketSize / 32) + 1].Select(x => Guid.NewGuid().ToString("N")).Aggregate("", (x, y) => x + y.ToString()).Substring(0, bucketSize);
                K = new object[(bucketSize / 32) + 1].Select(x => Guid.NewGuid().ToString("N")).Aggregate("", (x, y) => x + y.ToString()).Substring(0, bucketSize);
                L = new object[(bucketSize / 32) + 1].Select(x => Guid.NewGuid().ToString("N")).Aggregate("", (x, y) => x + y.ToString()).Substring(0, bucketSize);
                M = new object[(bucketSize / 32) + 1].Select(x => Guid.NewGuid().ToString("N")).Aggregate("", (x, y) => x + y.ToString()).Substring(0, bucketSize);
                N = new object[(bucketSize / 32) + 1].Select(x => Guid.NewGuid().ToString("N")).Aggregate("", (x, y) => x + y.ToString()).Substring(0, bucketSize);
                O = new object[(bucketSize / 32) + 1].Select(x => Guid.NewGuid().ToString("N")).Aggregate("", (x, y) => x + y.ToString()).Substring(0, bucketSize);
                P = new object[(bucketSize / 32) + 1].Select(x => Guid.NewGuid().ToString("N")).Aggregate("", (x, y) => x + y.ToString()).Substring(0, bucketSize);
                Q = new object[(bucketSize / 32) + 1].Select(x => Guid.NewGuid().ToString("N")).Aggregate("", (x, y) => x + y.ToString()).Substring(0, bucketSize);
                R = new object[(bucketSize / 32) + 1].Select(x => Guid.NewGuid().ToString("N")).Aggregate("", (x, y) => x + y.ToString()).Substring(0, bucketSize);
                S = new object[(bucketSize / 32) + 1].Select(x => Guid.NewGuid().ToString("N")).Aggregate("", (x, y) => x + y.ToString()).Substring(0, bucketSize);
                T = new object[(bucketSize / 32) + 1].Select(x => Guid.NewGuid().ToString("N")).Aggregate("", (x, y) => x + y.ToString()).Substring(0, bucketSize);
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

            public Task SendHugeData(string data)
            {
                return Task.CompletedTask;
            }

            public Task SendHugePayload(Payload[] payload)
            {
                return Task.CompletedTask;
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

        public class Base64ClientMiddleware : IClientMiddleware
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
                    await next(new ClientConnectionContext(context.ClientWebSocket, cryptoStream));
                }
            }
        }
    }
}
