// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Data;
using EventStore.Core.Index.Hashes;

namespace EventStore.Core.Services.PersistentSubscription.ConsumerStrategy {
	public class PinnedPersistentSubscriptionConsumerStrategy : PinnablePersistentSubscriptionConsumerStrategy {
		public PinnedPersistentSubscriptionConsumerStrategy(IHasher<string> streamHasher) : base(streamHasher) {
		}

		public override string Name {
			get { return SystemConsumerStrategies.Pinned; }
		}

		protected override string GetAssignmentSourceId(ResolvedEvent ev) {
			return GetSourceStreamId(ev);
		}
	}
}
