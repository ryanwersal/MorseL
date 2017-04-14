using System;

namespace WebSocketManager.Client
{
    public class InvocationHandler
    {
        public Action<object[]> Handler { get; set; }
        public Type[] ParameterTypes { get; set; }

        public InvocationHandler(Action<object[]> handler, Type[] parameterTypes)
        {
            Handler = handler;
            ParameterTypes = parameterTypes;
        }
    }
}
