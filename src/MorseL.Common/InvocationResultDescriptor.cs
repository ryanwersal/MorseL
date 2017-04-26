namespace MorseL.Common
{
    public class InvocationResultDescriptor : InvocationMessage
    {
        public object Result { get; set; }
        public string Error { get; set; }

        public override string ToString()
        {
            var resultType = Result?.GetType();
            return $"Id: {Id}, Result Type: {resultType}, Error: {Error}";
        }
    }
}
