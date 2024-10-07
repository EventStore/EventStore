// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Security.Claims;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.Transport.Tcp;

public interface ITcpDispatcher {
	TcpPackage? WrapMessage(Message message, byte version);

	Message UnwrapPackage(TcpPackage package, IEnvelope envelope, ClaimsPrincipal user,
		IReadOnlyDictionary<string, string> tokens, TcpConnectionManager connection, byte version);
}
