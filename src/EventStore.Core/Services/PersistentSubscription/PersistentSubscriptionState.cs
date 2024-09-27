// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.Services.PersistentSubscription {
	[Flags]
	public enum PersistentSubscriptionState {
		NotReady = 0x00,
		Behind = 0x01,
		OutstandingPageRequest = 0x02,
		ReplayingParkedMessages = 0x04,
		Live = 0x08,
	}
}
