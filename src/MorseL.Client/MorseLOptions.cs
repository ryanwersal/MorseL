using System;
using System.Collections.Generic;
using System.Text;

namespace MorseL.Client
{
    public class MorseLOptions
    {
        /// <summary>
        /// <para>
        /// Causes MorseL to throw an exception (handled via <see cref="Connection.Error"/>) when
        /// the connected hub issues a callback request that hasn't been registered.
        /// using <see cref="Connection.On(string, Type[], Action{object[]})"/>.
        /// </para>
        /// Defaults to false
        /// </summary>
        public bool ThrowOnMissingMethodRequest { get; set; } = false;

        /// <summary>
        /// <para>
        /// Causes MorseL to throw an exception (handled via <see cref="Connection.Error"/>) when
        /// the <see cref="Connection.Invoke"/> calls a non-existent hub method.
        /// </para>
        /// Defaults to false
        /// </summary>
        public bool ThrowOnMissingHubMethodInvoked { get; set; } = true;

        /// <summary>
        /// <para>
        /// Causes MorseL to throw an exception (handled via <see cref="Connection.Error"/>) when
        /// an invalid or unparseable message is sent from the hub. This is a critical and
        /// exceptional error case.
        /// </para>
        /// Defaults to true
        /// </summary>
        public bool ThrowOnInvalidMessage { get; set; } = true;

        /// <summary>
        /// <para>
        /// Causes MorseL to rethrow unobserved exceptions that occur during the receive
        /// loop. These can be captured via <see cref="Connection.Error"/>.
        /// </para>
        /// Defaults to true
        /// </summary>
        public bool RethrowUnobservedExceptions { get; set; } = true;

        /// <summary>
        /// <para>
        /// Causes MorseL to throw an exception (handled via <see cref="Connection.Error"/>) when
        /// a hub reports a method invocation was invalid. This is a critical and
        /// exceptional error case.
        /// </para>
        /// </summary>
        public bool ThrowOnInvalidRequest { get; set; } = true;
    }
}
