using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace MorseL.Internal
{
    internal class HubMethodDiscoverer<THub, TClient> where THub : Hub<TClient>
    {
        internal readonly IDictionary<string, HubMethodDescriptor> _methods = new Dictionary<string, HubMethodDescriptor>(StringComparer.OrdinalIgnoreCase);

        private readonly ILogger<HubMethodDiscoverer<THub, TClient>> _logger;

        public HubMethodDiscoverer(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<HubMethodDiscoverer<THub, TClient>>();

            DiscoverHubMethods();
        }

        public MethodInfo[] GetAllMethodInfo()
        {
            return _methods.Values.Select(d => d.MethodExecutor.MethodInfo).ToArray();
        }

        public bool TryGetHubMethodDescriptor(string methodName, out HubMethodDescriptor hubMethodDescriptor)
        {
            return _methods.TryGetValue(methodName, out hubMethodDescriptor);
        }

        private void DiscoverHubMethods()
        {
            var hubType = typeof(THub);
            foreach (var methodInfo in hubType.GetMethods().Where(IsHubMethod))
            {
                var methodName = methodInfo.Name;

                if (_methods.ContainsKey(methodName))
                {
                    throw new NotSupportedException($"Duplicate definitions of '{methodName}'. Overloading is not supported.");
                }

                var executor = ObjectMethodExecutor.Create(methodInfo, hubType.GetTypeInfo());
                _methods[methodName] = new HubMethodDescriptor(executor);

                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("Hub method '{methodName}' is bound", methodName);
                }
            }
        }

        private static bool IsHubMethod(MethodInfo methodInfo)
        {
            if (!methodInfo.IsPublic || methodInfo.IsSpecialName)
            {
                return false;
            }

            var baseDefinition = methodInfo.GetBaseDefinition().DeclaringType;
            var baseType = baseDefinition.GetTypeInfo().IsGenericType ? baseDefinition.GetGenericTypeDefinition() : baseDefinition;
            if (typeof(Hub<>) == baseType || typeof(object) == baseType)
            {
                return false;
            }

            return true;
        }
    }
}
