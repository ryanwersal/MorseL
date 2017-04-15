namespace WebSocketManager.Common
{
    public enum MessageType
    {
        Text,
        ClientMethodInvocation,
        InvocationResult,
        ConnectionEvent
    }

    public class Message
    {
        public MessageType MessageType { get; set; }
        public string Data { get; set; }

        public override string ToString()
        {
            return $"{MessageType}, {Data}";
        }
    }
}