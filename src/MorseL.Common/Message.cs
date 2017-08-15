using Newtonsoft.Json;

namespace MorseL.Common
{
    public enum MessageType
    {
        Text = 0,
        ClientMethodInvocation = 1,
        ConnectionEvent = 2,
        InvocationResult = 3
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