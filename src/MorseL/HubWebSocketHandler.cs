using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MorseL.Common;
using MorseL.Common.Serialization;
using MorseL.Diagnostics;
using MorseL.Internal;
using MorseL.Scaleout;
using MorseL.Sockets;

namespace MorseL
{
    public class HubWebSocketHandler<THub> : HubWebSocketHandler<THub, IClientInvoker> where THub : Hub<IClientInvoker>
    {
        public HubWebSocketHandler(IServiceProvider services, ILoggerFactory loggerFactory, IServiceScopeFactory serviceScopeFactory)
            : base(services, loggerFactory)
        {
        }
    }

    public class HubWebSocketHandler<THub, TClient> : WebSocketHandler where THub : Hub<TClient>
    {
        private readonly Dictionary<string, HubMethodDescriptor> _methods = new Dictionary<string, HubMethodDescriptor>(StringComparer.OrdinalIgnoreCase);

        private readonly IServiceProvider _services;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<HubWebSocketHandler<THub, TClient>> _logger;
        private readonly IAuthorizeData[] _authorizeData;
        private readonly IScaleoutBackPlane _scaleoutBackPlane;

        public HubWebSocketHandler(IServiceProvider services, ILoggerFactory loggerFactory) : base(services, loggerFactory)
        {
            _services = services;
            _logger = loggerFactory.CreateLogger<HubWebSocketHandler<THub, TClient>>();
            _serviceScopeFactory = services.GetRequiredService<IServiceScopeFactory>();
            _scaleoutBackPlane = services.GetService<IScaleoutBackPlane>();

            _authorizeData = typeof(THub).GetTypeInfo().GetCustomAttributes().OfType<IAuthorizeData>().ToArray();
            DiscoverHubMethods();
        }

        public override async Task OnConnectedAsync(Connection connection)
        {
            try
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var hubActivator = scope.ServiceProvider.GetRequiredService<IHubActivator<THub, TClient>>();
                    var hub = hubActivator.Create();
                    try
                    {
                        InitializeHub(hub, connection);
                        await hub.OnConnectedAsync(connection);
                    }
                    finally
                    {
                        hubActivator.Release(hub);
                    }
                }

                await _scaleoutBackPlane.Register(connection);
            }
            catch (Exception ex)
            {
                _logger.LogError(0, ex, $"Error when invoking OnConnectedAsync on hub for {connection.Id}");
                throw;
            }
        }

        public override async Task OnDisconnectedAsync(Connection connection, Exception exception)
        {
            try
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var hubActivator = scope.ServiceProvider.GetRequiredService<IHubActivator<THub, TClient>>();
                    var hub = hubActivator.Create();
                    try
                    {
                        InitializeHub(hub, connection);
                        await hub.OnDisconnectedAsync(exception);
                    }
                    finally
                    {
                        hubActivator.Release(hub);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(0, ex, $"Error when invoking OnDisconnectedAsync on hub for {connection.Id}");
                throw;
            }
            finally
            {
                await _scaleoutBackPlane.UnRegister(connection);
            }
        }

        private void InitializeHub(THub hub, Connection connection)
        {
            hub.Clients = ActivatorUtilities.CreateInstance<ClientsDispatcher>(_services);
            hub.Context = new HubCallerContext(connection);
            hub.Groups = new GroupsManager();
        }

        private void DiscoverHubMethods()
        {
            var hubType = typeof(THub);
            foreach (var methodInfo in hubType.GetMethods().Where(m => IsHubMethod(m)))
            {
                var methodName = methodInfo.Name;

                if (_methods.ContainsKey(methodName))
                {
                    throw new NotSupportedException($"Duplicate definitions of '{methodName}'. Overloading is not supported.");
                }

                var executor = ObjectMethodExecutor.Create(methodInfo, hubType.GetTypeInfo());
                _methods[methodName] = new HubMethodDescriptor(executor);

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Hub method '{methodName}' is bound", methodName);
                }
            }
        }

        private static bool IsHubMethod(MethodInfo methodInfo)
        {
            // TODO: Add more checks
            if (!methodInfo.IsPublic || methodInfo.IsSpecialName)
            {
                return false;
            }

            var baseDefinition = methodInfo.GetBaseDefinition().DeclaringType;
            var baseType = baseDefinition.GetTypeInfo().IsGenericType ? baseDefinition.GetGenericTypeDefinition() : baseDefinition;
            if (typeof(Hub<>) == baseType)
            {
                return false;
            }

            return true;
        }

        public override async Task ReceiveAsync(Connection connection, string serializedInvocationDescriptor)
        {
            var invocationDescriptor = Json.DeserializeInvocationDescriptor(serializedInvocationDescriptor, _methods.Values.Select(d => d.MethodExecutor.MethodInfo).ToArray());

            if (invocationDescriptor == null)
            {
                await connection.Channel.SendMessageAsync(new Message()
                {
                    MessageType = MessageType.Text,
                    Data = $"Cannot find method to match inbound request for {connection.Id}"
                }).ConfigureAwait(false);
                return;
            }

            HubMethodDescriptor descriptor;
            if (!_methods.TryGetValue(invocationDescriptor.MethodName, out descriptor))
            {
                await connection.Channel.SendMessageAsync(new Message()
                {
                    MessageType = MessageType.Text,
                    Data = $"Cannot find method {invocationDescriptor.MethodName} to match inbound request for {connection.Id}"
                }).ConfigureAwait(false);
                return;
            }

            var result = await Invoke(descriptor, connection, invocationDescriptor);

            var message = new Message
            {
                MessageType = MessageType.InvocationResult,
                Data = Json.SerializeObject(result)
            };

            await connection.Channel.SendMessageAsync(message);
        }

        private async Task<InvocationResultDescriptor> Invoke(HubMethodDescriptor descriptor, Connection connection, InvocationDescriptor invocationDescriptor)
        {
            var invocationResult = new InvocationResultDescriptor
            {
                Id = invocationDescriptor.Id
            };

            var methodExecutor = descriptor.MethodExecutor;
            var methodName = methodExecutor.MethodInfo.Name;

            using (_logger.Tracer($"{this.GetType()}.Invoke({methodName}, ...)"))
            {
                // TODO: Authenticate on method invocation?

                // TODO: This abuses the resource context in authorize to pass the connection ID
                // Come up with a better way to retain contextual information of the user being authenticated?
                // Perhaps add a connection ID claim to the user? :/
                if (!await AuthorizeAsync(descriptor.AuthorizeData, connection.User, connection.Id))
                {
                    _logger.LogError($"Unauthorized access for Hub method '{methodName}' for {connection.Id}");
                    invocationResult.Error = $"Unauthorized access for Hub method '{methodName}'";
                    return invocationResult;
                }

                // TODO: Should we challenge? What does that mean in the context of hub invocation?

                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var hubActivator = scope.ServiceProvider.GetRequiredService<IHubActivator<THub, TClient>>();
                    var hub = hubActivator.Create();

                    try
                    {
                        InitializeHub(hub, connection);

                        object result = null;
                        if (methodExecutor.IsMethodAsync)
                        {
                            if (methodExecutor.TaskGenericType == null)
                            {
                                await (Task) methodExecutor.Execute(hub, invocationDescriptor.Arguments);
                            }
                            else
                            {
                                result = await methodExecutor.ExecuteAsync(hub, invocationDescriptor.Arguments);
                            }
                        }
                        else
                        {
                            result = methodExecutor.Execute(hub, invocationDescriptor.Arguments);
                        }

                        invocationResult.Result = result;
                    }
                    catch (TargetInvocationException ex)
                    {
                        _logger.LogError(0, ex, $"Failed to invoke hub method for {connection.Id}");
                        invocationResult.Error = ex.InnerException.Message;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(0, ex, $"Failed to invoke hub method for {connection.Id}");
                        invocationResult.Error = ex.Message;
                    }
                    finally
                    {
                        hubActivator.Release(hub);
                    }
                }

                return invocationResult;
            }
        }

        private async Task<bool> AuthorizeAsync(IAuthorizeData[] authorizeData, ClaimsPrincipal user, object context)
        {
            // Default to Hub's IAuthorizeData if none specified
            var effectiveAuthorizeData = authorizeData?.Length > 0 ? authorizeData : _authorizeData;

            if (effectiveAuthorizeData != null && effectiveAuthorizeData.Length > 0)
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var authorizationService = scope.ServiceProvider.GetService<IAuthorizationService>();
                    var provider = scope.ServiceProvider.GetService<IAuthorizationPolicyProvider>();

                    if (authorizationService != null && provider != null)
                    {
                        var policy = await AuthorizationPolicy.CombineAsync(provider, effectiveAuthorizeData);

                        if (!await authorizationService.AuthorizeAsync(user, context, policy))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private class HubMethodDescriptor
        {
            public HubMethodDescriptor(ObjectMethodExecutor methodExecutor)
            {
                MethodExecutor = methodExecutor;
                ParameterTypes = methodExecutor.ActionParameters.Select(p => p.ParameterType).ToArray();
                AuthorizeData = methodExecutor.MethodInfo.GetCustomAttributes().OfType<AuthorizeAttribute>().ToArray();
            }

            public ObjectMethodExecutor MethodExecutor { get; }

            public Type[] ParameterTypes { get; }

            public IAuthorizeData[] AuthorizeData { get; }
        }
    }
}
