﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Claims;
using System.Threading;
using MorseL.Sockets;

namespace MorseL
{
    public class HubCallerContext
    {
        public HubCallerContext(Connection connection)
        {
            Connection = connection;
        }

        public Connection Connection { get; }

        public ClaimsPrincipal User => Connection.User;

        public string ConnectionId => Connection.Id;

        public CancellationToken ConnectionCancellationToken => Connection.ConnectionCancellationToken;
    }
}
