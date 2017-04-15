namespace WebSocketManager.Common
{
    public class InvocationResultDescriptor : InvocationMessage
    {
        public object Result { get; set; }
        public string Error { get; set; }
    }
}
