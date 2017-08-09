using Microsoft.Extensions.Options;
using MorseL.Scaleout;
using System;
using System.Collections.Generic;
using System.Text;

namespace MorseL.Extensions
{
    public class MorseLOptions
    {
        /// <summary>
        /// <para>
        /// Causes MorseL to throw an exception when a client attempts to invoke a
        /// non-existent method on the hub.
        /// </para>
        /// Defaults to false
        /// </summary>
        public bool ThrowOnMissingHubMethodRequest { get; set; } = false;

        /// <summary>
        /// <para>
        /// Causes MorseL to throw an exception when the hub attempts to invoke
        /// a client method that the client has subscribed for.
        /// </para>
        /// Defaults to false
        /// </summary>
        public bool ThrowOnMissingClientMethodInvoked { get; set; } = false;

        /// <summary>
        /// <para>
        /// Causes MorseL to throw an exception (handled via <see cref="Connection.Error"/>) when
        /// an invalid or unparseable message is sent from the client. This is a critical and
        /// exceptional error case.
        /// </para>
        /// Defaults to true
        /// </summary>
        public bool ThrowOnInvalidMessage { get; set; } = true;
    }
}
