using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;

namespace MorseL.Internal
{
    internal class HubMethodDescriptor
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
