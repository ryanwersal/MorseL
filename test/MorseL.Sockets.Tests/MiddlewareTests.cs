using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MorseL.Common;
using MorseL.Common.Serialization;
using MorseL.Shared.Tests;
using MorseL.Sockets.Middleware;
using Xunit;
using Moq;

namespace MorseL.Sockets.Test
{
    [Trait("Target", "Middleware")]
    public class MiddlewareTests
    {
        [Theory]
        [InlineData("To be or not to be, that is the question")]
        public async Task SendAsyncMiddlewareIsCalledOnArbitrarySendMessageAsync(string text)
        {
            var socket = new LinkedFakeSocket();
            ILoggerFactory loggerFactory = new LoggerFactory();
            var webSocketChannel = new WebSocketChannel(socket, new []{ new Base64Middleware() }, loggerFactory);
            await webSocketChannel.SendMessageAsync(new Message
            {
                Data = text,
                MessageType = MessageType.Text
            });

            var encodedMessage = await socket.ReadToEndAsync();
            var message = MessageSerializer.Deserialize<Message>(Encoding.UTF8.GetString(Convert.FromBase64String(encodedMessage)));

            Assert.Equal(message.Data, text);
        }

        [Theory]
        [InlineData("To be or not to be, that is the question")]
        public async Task SendAsyncMiddlewareIsCalledOnArbitrarySendAsync(string text)
        {
            var socket = new LinkedFakeSocket();
            ILoggerFactory loggerFactory = new LoggerFactory();
            var webSocketChannel = new WebSocketChannel(socket, new[] { new Base64Middleware() }, loggerFactory);
            await webSocketChannel.SendAsync(new MemoryStream(Encoding.UTF8.GetBytes(text)));

            var encodedMessage = await socket.ReadToEndAsync();
            var message = Encoding.UTF8.GetString(Convert.FromBase64String(encodedMessage));

            Assert.Equal(message, text);
        }

        [Theory]
        [InlineData("To be or not to be, that is the question")]
        public async Task SendAsyncMiddlewareCalledInOrderOnArbitrarySendMessageAsync(string text)
        {
            var middlewares = new List<IMiddleware>();
            for (int i = 0; i < 10; i++)
            {
                middlewares.Add(new IncrementalMiddleware());
            }

            var socket = new LinkedFakeSocket();
            ILoggerFactory loggerFactory = new LoggerFactory();
            var webSocketChannel = new WebSocketChannel(socket, middlewares, loggerFactory);
            await webSocketChannel.SendMessageAsync(new Message
            {
                Data = text,
                MessageType = MessageType.Text
            });

            foreach (var m in middlewares)
            {
                var middleware = (IncrementalMiddleware) m;
                Assert.Equal(middleware.Id, middleware.CalledAt);
            }
        }

        [Theory]
        [InlineData("To be or not to be, that is the question")]
        public async Task SendAsyncMiddlewareCalledInOrderOnArbitrarySendAsync(string text)
        {
            var middlewares = new List<IMiddleware>();
            for (int i = 0; i < 10; i++)
            {
                middlewares.Add(new IncrementalMiddleware());
            }

            var socket = new LinkedFakeSocket();
            ILoggerFactory loggerFactory = new LoggerFactory();
            var webSocketChannel = new WebSocketChannel(socket, middlewares, loggerFactory);
            await webSocketChannel.SendAsync(new MemoryStream(Encoding.UTF8.GetBytes(text)));

            foreach (var m in middlewares)
            {
                var middleware = (IncrementalMiddleware)m;
                Assert.Equal(middleware.Id, middleware.CalledAt);
            }
        }

        [Fact]
        public async Task Additional_Middleware_Should_Be_Given_The_Mutated_Context()
        {
            var middleware = new List<MorseL.Sockets.Middleware.IMiddleware>
            {
                new TestMiddleware("First"),
                new TestMiddleware("Second"),
                new TestMiddleware("Third")
            };

            string originalContents = "test stream data";
            string contents = null;

            var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(originalContents));
            var mockChannel = new Mock<IChannel>();
            var connection = new Connection("connectionId", mockChannel.Object);
            var connectionContext = new MorseL.Sockets.Middleware.ConnectionContext(connection, inputStream);

            var socket = new LinkedFakeSocket();
            ILoggerFactory loggerFactory = new LoggerFactory();
            var webSocketChannel = new WebSocketChannel(socket, middleware, loggerFactory);
            
            var delegator = webSocketChannel.BuildMiddlewareDelegate(middleware.GetEnumerator(), async stream => {
                using (var memStream = new MemoryStream())
                {
                    await stream.CopyToAsync(memStream);
                    contents = Encoding.UTF8.GetString(memStream.ToArray());
                }
            });

            await delegator.Invoke(connectionContext);

            string expectedResults = $"SENTThird:SENTSecond:SENTFirst:{originalContents}";

            Assert.Equal(expectedResults, contents);
        }

        private class TestMiddleware : MorseL.Sockets.Middleware.IMiddleware
        {
            private string _prefix;
            public TestMiddleware(string prefix)
            {
                _prefix = prefix;
            }

            public async Task SendAsync(MorseL.Sockets.Middleware.ConnectionContext context, MorseL.Sockets.Middleware.MiddlewareDelegate next)
            {
                string contents = null;
                using (var memStream = new MemoryStream())
                {
                    await context.Stream.CopyToAsync(memStream);
                    contents = Encoding.UTF8.GetString(memStream.ToArray());
                }

                contents = $"SENT{_prefix}:{contents}";
                await next(new MorseL.Sockets.Middleware.ConnectionContext(context.Connection, new MemoryStream(Encoding.UTF8.GetBytes(contents))));
            }

            public async Task ReceiveAsync(MorseL.Sockets.Middleware.ConnectionContext context, MorseL.Sockets.Middleware.MiddlewareDelegate next)
            {
                string contents = null;
                using (var memStream = new MemoryStream())
                {
                    await context.Stream.CopyToAsync(memStream);
                    contents = Encoding.UTF8.GetString(memStream.ToArray());
                }

                contents = $"RECEIVED{_prefix}:{contents}";
                await next(new MorseL.Sockets.Middleware.ConnectionContext(context.Connection, new MemoryStream(Encoding.UTF8.GetBytes(contents))));
            }
        }

        private class IncrementalMiddleware : IMiddleware
        {
            private static int _creationOrder = 0;
            private static int _callOrder = 0;

            public readonly int Id = _creationOrder++;
            public int CalledAt { get; private set; }

            public async Task SendAsync(ConnectionContext context, MiddlewareDelegate next)
            {
                CalledAt = _callOrder++;
                await next(context);
            }

            public async Task ReceiveAsync(ConnectionContext context, MiddlewareDelegate next)
            {
                await next(context);
            }
        }

        // TODO : Receive middleware tests...

        private class Base64Middleware : IMiddleware
        {
            public async Task SendAsync(ConnectionContext context, MiddlewareDelegate next)
            {
                using (var reader = new StreamReader(context.Stream))
                {
                    var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(await reader.ReadToEndAsync()));
                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(data)))
                    {
                        await next(new ConnectionContext(context.Connection, stream));
                    }
                }
            }

            public async Task ReceiveAsync(ConnectionContext context, MiddlewareDelegate next)
            {
                using (var reader = new StreamReader(context.Stream))
                {
                    var data = Convert.FromBase64String(await reader.ReadToEndAsync());
                    using (var stream = new MemoryStream(data))
                    {
                        await next(new ConnectionContext(context.Connection, stream));
                    }
                }
            }
        }
    }
}
