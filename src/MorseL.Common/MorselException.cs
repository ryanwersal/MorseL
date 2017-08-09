using System;
using System.Collections.Generic;
using System.Text;

namespace MorseL.Common
{
    public class MorseLException : Exception
    {
        public MorseLException()
        {
        }

        public MorseLException(string message) : base(message)
        {
        }

        public MorseLException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
