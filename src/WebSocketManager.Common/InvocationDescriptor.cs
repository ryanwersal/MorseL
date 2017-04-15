namespace WebSocketManager.Common
{
    public class InvocationDescriptor : InvocationMessage
    {
        public string MethodName { get; set; }

        public object[] Arguments { get; set; }
    }
}