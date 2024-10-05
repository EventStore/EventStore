// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing.Partitioning;

public class NoopStatePartitionSelector : StatePartitionSelector {
	public override string GetStatePartition(EventReaderSubscriptionMessage.CommittedEventReceived @event) {
		return "";
	}

	public override bool EventReaderBasePartitionDeletedIsSupported() {
		return false;
	}
}
