using System;
using System.Collections.Generic;
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

        private readonly Mock<IServiceProvider> HubActivatorMockProvider = new Mock<IServiceProvider>();

        public ServicesMocker()
        {
            ServiceProviderMock.Setup(m => m.GetService(It.Is<Type>(t => t == typeof(IServiceScopeFactory))))
                        .Returns(ServiceScopeFactoryMock.Object);

            var serviceScopeMock = new Mock<IServiceScope>();
            serviceScopeMock.Setup(m => m.ServiceProvider).Returns(ServiceProviderMock.Object);
            ServiceScopeFactoryMock.Setup(m => m.CreateScope()).Returns(serviceScopeMock.Object);

            ServiceProviderMock.Setup(m => m.GetService(It.Is<Type>(t => t == typeof(IBackplane)))).Returns(BackplaneMock.Object);

            MorseLOptionsMock.Setup(m => m.Value).Returns(new MorseLOptions());
            ServiceProviderMock.Setup(m => m.GetService(It.Is<Type>(t => t == typeof(IOptions<MorseLOptions>))))
                        .Returns(MorseLOptionsMock.Object);

            ServiceProviderMock.Setup(m => m.GetService(It.Is<Type>(t => t == typeof(WebSocketConnectionManager))))
                        .Returns(new WebSocketConnectionManager());

            ServiceProviderMock.Setup(m => m.GetService(It.Is<Type>(t => t == typeof(ILogger<ClientsDispatcher>))))
                        .Returns(Mock.Of<ILogger<ClientsDispatcher>>());

            var loggerMock = new Mock<ILogger>();
            LoggerFactoryMock.Setup(m => m.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);
        }

        public Mock<IHubActivator<THub, IClientInvoker>> RegisterHub<THub>() where THub : Hub<IClientInvoker>, new()
        {
            var hubActivatorMock = new Mock<IHubActivator<THub, IClientInvoker>>();
            hubActivatorMock.Setup(m => m.Create()).Returns(new THub());

            HubActivatorMockProvider.Setup(m => m.GetService(It.Is<Type>(t => t == typeof(THub))))
                                    .Returns(hubActivatorMock);

            ServiceProviderMock.Setup(m => m.GetService(It.Is<Type>(t => t == typeof(IHubActivator<THub, IClientInvoker>))))
                        .Returns(hubActivatorMock.Object);

            return hubActivatorMock;
        }

        public Mock<IHubActivator<THub, IClientInvoker>> GetHubActivator<THub>() where THub : Hub<IClientInvoker>
        {
            return HubActivatorMockProvider.Object.GetService(typeof(THub)) as Mock<IHubActivator<THub, IClientInvoker>>;
        }
    }
}
