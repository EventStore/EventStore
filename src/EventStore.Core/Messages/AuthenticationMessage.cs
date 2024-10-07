// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Messaging;

namespace EventStore.Core.Messages;

public static partial class AuthenticationMessage {
	[DerivedMessage(CoreMessage.Authentication)]
	public partial class AuthenticationProviderInitialized : Message {
	}

	[DerivedMessage(CoreMessage.Authentication)]
	public partial class AuthenticationProviderInitializationFailed : Message {
	}
}
