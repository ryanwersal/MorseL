using System;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MorseL.Extensions;
using MorseL.Scaleout;
using MorseL.Sockets;

namespace MorseL.Shared.Tests
{
    public class ServicesMocker
    {
        public readonly Mock<IServiceProvider> ServiceProviderMock = new Mock<IServiceProvider>();
        public readonly Mock<IServiceScopeFactory> ServiceScopeFactoryMock = new Mock<IServiceScopeFactory>();
        public readonly Mock<IBackplane> BackplaneMock = new Mock<IBackplane>();
        public readonly Mock<IOptions<MorseLOptions>> MorseLOptionsMock = new Mock<IOptions<MorseLOptions>>();
        public readonly Mock<ILoggerFactory> LoggerFactoryMock = new Mock<ILoggerFactory>();
        public readonly Mock<IChannel> ChannelMock = new Mock<IChannel>();
        public readonly Mock<IWebSocketConnectionManager> WebSocketConnectionManagerMock = new Mock<IWebSocketConnectionManager>();

        public readonly Mock<HttpContext> HttpContextMock = new Mock<HttpContext>();
        public readonly Mock<ClaimsPrincipal> UserClaimsPrincipalMock = new Mock<ClaimsPrincipal>();
        public readonly Mock<WebSocketManager> WebSocketManagerMock = new Mock<WebSocketManager>();
        public readonly Mock<WebSocket> WebSocketMock = new Mock<WebSocket>();

        public readonly string ConnectionId = Guid.NewGuid().ToString();

        private readonly Mock<IServiceProvider> _hubActivatorProviderMock = new Mock<IServiceProvider>();

        public ServicesMocker()
        {
            RegisterService(ServiceProviderMock);
            RegisterService(ServiceScopeFactoryMock);
            RegisterService(LoggerFactoryMock);
            RegisterService(WebSocketConnectionManagerMock);

            var serviceScopeMock = new Mock<IServiceScope>();
            serviceScopeMock.Setup(m => m.ServiceProvider).Returns(ServiceProviderMock.Object);
            ServiceScopeFactoryMock.Setup(m => m.CreateScope()).Returns(serviceScopeMock.Object);

            RegisterService(BackplaneMock);

            MorseLOptionsMock.Setup(m => m.Value).Returns(new MorseLOptions());
            RegisterService(MorseLOptionsMock);

            RegisterService<ILogger<ClientsDispatcher>>();

            var connection = new Connection(ConnectionId, ChannelMock.Object);

            WebSocketConnectionManagerMock.Setup(m => m.AddConnection(It.IsAny<IChannel>())).Returns(connection);
            WebSocketConnectionManagerMock.Setup(m => m.GetConnectionById(It.Is<string>(s => s == ConnectionId)))
                .Returns(connection);
            WebSocketConnectionManagerMock.Setup(m => m.GetConnection(It.Is<WebSocket>(ws => ws == WebSocketMock.Object)))
                .Returns(connection);

            var loggerMock = new Mock<ILogger>();
            LoggerFactoryMock.Setup(m => m.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);

            HttpContextMock.Setup(m => m.WebSockets).Returns(WebSocketManagerMock.Object);
            HttpContextMock.Setup(m => m.RequestServices).Returns(ServiceProviderMock.Object);
            HttpContextMock.Setup(m => m.User).Returns(UserClaimsPrincipalMock.Object);

            WebSocketMock.Setup(m => m.State).Returns(WebSocketState.None);
            WebSocketMock.Setup(m => m.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true)));

            WebSocketManagerMock.Setup(m => m.IsWebSocketRequest).Returns(true);
            WebSocketManagerMock.Setup(m => m.AcceptWebSocketAsync()).Returns(Task.FromResult(WebSocketMock.Object));
        }

        public Mock<TService> RegisterService<TService>(DefaultValue defaultValue = DefaultValue.Empty) where TService : class
        {
            var mock = new Mock<TService> { DefaultValue = defaultValue };

            RegisterService(mock);

            return mock;
        }

        public void RegisterService<TService>(Mock<TService> mock) where TService : class
        {
            ServiceProviderMock.Setup(m => m.GetService(It.Is<Type>(t => t == typeof(TService)))).Returns(mock.Object);
        }

        public Mock<IHubActivator<THub, IClientInvoker>> RegisterHub<THub>(THub hub = null) where THub : Hub<IClientInvoker>, new()
        {
            var hubActivatorMock = new Mock<IHubActivator<THub, IClientInvoker>>();
            hubActivatorMock.Setup(m => m.Create()).Returns(hub ?? new THub());

            _hubActivatorProviderMock.Setup(m => m.GetService(It.Is<Type>(t => t == typeof(THub))))
                                    .Returns(hubActivatorMock);

            ServiceProviderMock.Setup(m => m.GetService(It.Is<Type>(t => t == typeof(IHubActivator<THub, IClientInvoker>))))
                        .Returns(hubActivatorMock.Object);

            return hubActivatorMock;
        }

        public Mock<IHubActivator<THub, IClientInvoker>> GetHubActivator<THub>() where THub : Hub<IClientInvoker>
        {
            return _hubActivatorProviderMock.Object.GetService(typeof(THub)) as Mock<IHubActivator<THub, IClientInvoker>>;
        }
    }
}
