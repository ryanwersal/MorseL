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
using MorseL.Extensions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

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
        private readonly IBackplane _backplane;
        private readonly MorseLOptions _morselOptions;

        public HubWebSocketHandler(IServiceProvider services, ILoggerFactory loggerFactory) : base(services, loggerFactory)
        {
            _services = services;
            _logger = loggerFactory.CreateLogger<HubWebSocketHandler<THub, TClient>>();
            _serviceScopeFactory = services.GetRequiredService<IServiceScopeFactory>();
            _backplane = services.GetService<IBackplane>();
            _morselOptions = services.GetService<IOptions<MorseLOptions>>().Value;

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

                await _backplane.OnClientConnectedAsync(connection.Id);
                _backplane.OnMessage += async (connectionId, message) => {
                    if (connectionId.Equals(connection.Id)) {
                        await connection.Channel.SendMessageAsync(message);
                    }
                };
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
                await _backplane.OnClientDisconnectedAsync(connection.Id);
            }
        }

        private void InitializeHub(THub hub, Connection connection)
        {
            hub.Clients = ActivatorUtilities.CreateInstance<ClientsDispatcher>(_services);
            hub.Client = hub.Clients.Client(connection.Id);
            hub.Context = new HubCallerContext(connection);
            hub.Groups = ActivatorUtilities.CreateInstance<GroupsDispatcher>(_services);
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
            InvocationDescriptor invocationDescriptor = null;
            try
            {
                invocationDescriptor = Json.DeserializeInvocationDescriptor(serializedInvocationDescriptor, _methods.Values.Select(d => d.MethodExecutor.MethodInfo).ToArray());
            }
            catch (Exception e)
            {
                await HandleUnparseableReceivedInvocationDescriptor(connection, serializedInvocationDescriptor, e);
                return;
            }

            if (invocationDescriptor == null)
            {
                // Valid JSON but unparseable into a known, typed invocation descriptor (unknown method name, invalid parameters)
                await HandleInvalidReceivedInvocationDescriptor(connection, serializedInvocationDescriptor);
                return;
            }

            HubMethodDescriptor descriptor;
            if (!_methods.TryGetValue(invocationDescriptor.MethodName, out descriptor))
            {
                // Really strange and unlikely, valid JSON and was known but not found here
                // Likely not possible
                await HandleMissingReceivedInvocationDescriptor(connection, invocationDescriptor);
            }

            var result = await Invoke(descriptor, connection, invocationDescriptor);

            var message = new Message
            {
                MessageType = MessageType.InvocationResult,
                Data = Json.SerializeObject(result)
            };

            await connection.Channel.SendMessageAsync(message);
        }

        private Task HandleMissingReceivedInvocationDescriptor(Connection connection, JObject invocationDescriptor)
        {
            var invocationId = invocationDescriptor.Value<string>("Id");

            var methodName = invocationDescriptor.Value<string>("MethodName");
            methodName = string.IsNullOrWhiteSpace(methodName) ? "[Invalid Method Name]" : methodName;

            JArray argumentTokenList = invocationDescriptor.Value<JArray>("Arguments");
            var argumentList = argumentTokenList?.Count > 0 ? String.Join(", ", argumentTokenList) : "[No Parameters]";

            return HandleMissingReceivedInvocationDescriptor(connection, invocationId, methodName, argumentList);
        }

        private Task HandleMissingReceivedInvocationDescriptor(Connection connection, InvocationDescriptor invocationDescriptor)
        {
            var methodName = invocationDescriptor.MethodName;
            methodName = string.IsNullOrWhiteSpace(methodName) ? "[Invalid Method Name]" : methodName;

            object[] argumentTokenList = invocationDescriptor.Arguments;
            var argumentList = argumentTokenList?.Length > 0 ? String.Join(", ", argumentTokenList) : "[No Parameters]";

            return HandleMissingReceivedInvocationDescriptor(connection, invocationDescriptor.Id, methodName, argumentList);
        }

        private Task HandleMissingReceivedInvocationDescriptor(Connection connection, string invocationId, string methodName, string argumentList)
        {
            if (_morselOptions.ThrowOnMissingHubMethodRequest)
            {
                throw new MorseLException($"Invalid method request received from {connection.Id}; method is \"{methodName}({argumentList})\"");
            }

            _logger?.LogDebug($"Invalid method request received from {connection.Id}; method is \"{methodName}({argumentList})\"");

            return connection.Channel.SendMessageAsync(new Message()
            {
                MessageType = MessageType.InvocationResult,
                Data = Json.SerializeObject(new InvocationResultDescriptor
                {
                    Id = invocationId,
                    Error = $"Cannot find method \"{methodName}({argumentList})\""
                })
            });
        }

        private Task HandleInvalidReceivedInvocationDescriptor(Connection connection, string serializedInvocationDescriptor)
        {
            // Try to create a typeless descriptor
            JObject invocationDescriptor = null;
            try
            {
                invocationDescriptor = Json.Deserialize<JObject>(serializedInvocationDescriptor);
            }
            catch { }

            // We were able to make heads or tails of the invocation descriptor
            if (invocationDescriptor != null && invocationDescriptor.Value<string>("Id") != null)
            {
                return HandleMissingReceivedInvocationDescriptor(connection, invocationDescriptor);
            }

            return HandleUnparseableReceivedInvocationDescriptor(connection, serializedInvocationDescriptor);
        }

        private Task HandleUnparseableReceivedInvocationDescriptor(Connection connection, string serializedInvocationDescriptor, Exception exception = null)
        {
            // Hack so we don't have refactor the client to send the full Message wrapped payload
            // Right now it only sends serialized InvocationDescriptors, whereas Hubs send a Message
            // wrapped payload that can contain other types of messages within Data.
            // However, we want to be able to handle specific error cases clients inform us about
            // (ie. hubs sending invalid invocation requests) so we specifically check here for now
            // In any event, if we are here, we are definitely in an error case so the small chance
            // that we have a false positive on a match here will still be an error case.
            if (serializedInvocationDescriptor.StartsWith("Error: Cannot find method"))
            {
                if (_morselOptions.ThrowOnMissingClientMethodInvoked)
                {
                    throw new MorseLException($"{serializedInvocationDescriptor} from {connection.Id}");
                }

                _logger?.LogError($"{serializedInvocationDescriptor} from {connection.Id}");

                return Task.CompletedTask;
            } else if (serializedInvocationDescriptor.StartsWith("Error: Invalid message"))
            {
                // We have no idea what we have for a message
                if (_morselOptions.ThrowOnInvalidMessage)
                {
                    throw new MorseLException($"Invalid message sent \"{serializedInvocationDescriptor}\" and unhandled by {connection.Id}");
                }

                _logger?.LogError($"Invalid message sent \"{serializedInvocationDescriptor}\" and unhandled by {connection.Id}");

                return Task.CompletedTask;
            }

            // We have no idea what we have for a message
            if (_morselOptions.ThrowOnInvalidMessage)
            {
                throw new MorseLException($"Invalid message received \"{serializedInvocationDescriptor}\" from {connection.Id}");
            }

            _logger?.LogError(new EventId(), exception, $"Invalid message \"{serializedInvocationDescriptor}\" received from {connection.Id}");

            // TODO : Move to a Error type that can be handled specifically
            // Since we don't have an invocation descriptor we can't return an invocation result
            return connection.Channel.SendMessageAsync(new Message()
            {
                MessageType = MessageType.Text,
                Data = $"Invalid message \"{serializedInvocationDescriptor}\""
            });
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
