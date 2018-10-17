﻿using System;
using MorseL.Internal;
using MorseL.Shared.Tests;
using Xunit;

namespace MorseL.Tests
{
    public class HubMethodDiscovererTests
    {
        private readonly ServicesMocker Mocker = new ServicesMocker();

        public class EmptyHub : Hub<IClientInvoker> { }

        [Fact]
        public void EmptyHubFindsNoAvailableMethods()
        {
            var discoverer = new HubMethodDiscoverer<EmptyHub, IClientInvoker>(
                Mocker.LoggerFactoryMock.Object);
            Assert.Empty(discoverer._methods);
        }

        public class PropertyHub : Hub<IClientInvoker>
        {
            public string Foo { get; set; }
        }

        [Fact]
        public void HubWithOnlyPropertiesFindsNoAvailableMethods()
        {
            var discoverer = new HubMethodDiscoverer<PropertyHub, IClientInvoker>(
                Mocker.LoggerFactoryMock.Object);
            Assert.Empty(discoverer._methods);
        }

        public class AccessibilityHub : Hub<IClientInvoker>
        {
            public void PublicMethod() {}
            protected void ProtectedMethod() {}
            private void PrivateMethod() {}
            internal void InternalMethod() {}
        }

        [Fact]
        public void ShouldDiscoverPublicMethod()
        {
            var discoverer = new HubMethodDiscoverer<AccessibilityHub, IClientInvoker>(
                Mocker.LoggerFactoryMock.Object);
            Assert.Contains(discoverer._methods, m => m.Key == "PublicMethod");
            Assert.Equal(discoverer._methods.Count, 1);
        }

        [Fact]
        public void ShouldNotDiscoverProtectedMethod()
        {
            var discoverer = new HubMethodDiscoverer<AccessibilityHub, IClientInvoker>(
                Mocker.LoggerFactoryMock.Object);
            Assert.DoesNotContain(discoverer._methods, m => m.Key == "ProtectedMethod");
            Assert.Equal(discoverer._methods.Count, 1);
        }

        [Fact]
        public void ShouldNotDiscoverPrivateMethod()
        {
            var discoverer = new HubMethodDiscoverer<AccessibilityHub, IClientInvoker>(
                Mocker.LoggerFactoryMock.Object);
            Assert.DoesNotContain(discoverer._methods, m => m.Key == "PrivateMethod");
            Assert.Equal(discoverer._methods.Count, 1);
        }

        [Fact]
        public void ShouldNotDiscoverInternalMethod()
        {
            var discoverer = new HubMethodDiscoverer<AccessibilityHub, IClientInvoker>(
                Mocker.LoggerFactoryMock.Object);
            Assert.DoesNotContain(discoverer._methods, m => m.Key == "InternalMethod");
            Assert.Equal(discoverer._methods.Count, 1);
        }

        public class BaseHub : Hub<IClientInvoker>
        {
            public void BaseMethod() {}
        }

        public class SubclassHub : BaseHub 
        {
            public void SubclassMethod() {}
        }

        [Fact]
        public void InheritingFromAHub_ShouldDiscoverOwnPublicMethod()
        {
            var discoverer = new HubMethodDiscoverer<SubclassHub, IClientInvoker>(
                Mocker.LoggerFactoryMock.Object);
            Assert.Contains(discoverer._methods, m => m.Key == "SubclassMethod");
            Assert.Equal(discoverer._methods.Count, 2);
        }

        [Fact]
        public void InheritingFromAHub_ShouldDiscoverBaseHubPublicMethod()
        {
            var discoverer = new HubMethodDiscoverer<SubclassHub, IClientInvoker>(
                Mocker.LoggerFactoryMock.Object);
            Assert.Contains(discoverer._methods, m => m.Key == "BaseMethod");
            Assert.Equal(discoverer._methods.Count, 2);
        }
    }
}
