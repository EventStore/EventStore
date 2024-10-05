// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing.Partitioning;

public class ByHandleStatePartitionSelector : StatePartitionSelector {
	private readonly IProjectionStateHandler _handler;

	public ByHandleStatePartitionSelector(IProjectionStateHandler handler) {
		_handler = handler;
	}

	public override string GetStatePartition(EventReaderSubscriptionMessage.CommittedEventReceived @event) {
		return _handler.GetStatePartition(@event.CheckpointTag, @event.EventCategory, @event.Data);
	}

	public override bool EventReaderBasePartitionDeletedIsSupported() {
		return false;
	}
}
