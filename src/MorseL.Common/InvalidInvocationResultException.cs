using System;
using System.Collections.Generic;
using System.Text;

namespace MorseL.Common
{
    public class InvalidInvocationResultException : MorseLException
    {
        public string InvocationResultDescriptor { get; set; }
        public string RequestId { get; set; }

        public InvalidInvocationResultException(string invocationResultDescriptor, string requestId) : base("Invalid result descriptor")
        {
            InvocationResultDescriptor = invocationResultDescriptor;
            RequestId = requestId;
        }

        public InvalidInvocationResultException(string message, string invocationResultDescriptor, string requestId) : base(message)
        {
            InvocationResultDescriptor = invocationResultDescriptor;
            RequestId = requestId;
        }
    }
}
