// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.Services.PersistentSubscription.ConsumerStrategy.PinnedState {
	internal struct BucketAssignment {
		internal enum BucketState {
			Unassigned,
			Assigned,
			Disconnected
		}

		public Guid NodeId { get; set; }

		public BucketState State { get; set; }

		public int InFlightCount { get; set; }

		public Node Node { get; set; }
	}
}
