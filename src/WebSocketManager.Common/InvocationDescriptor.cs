namespace WebSocketManager.Common
{
    public class InvocationDescriptor : InvocationMessage
    {
        public string MethodName { get; set; }

        public object[] Arguments { get; set; }

        public override string ToString()
        {
            return $"{MethodName}, {Arguments?.Length ?? 0} Arguments";
        }
    }
}