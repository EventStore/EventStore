// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Projections.Core.Messages {
	// The name of this enum and its members are used for metrics
	public enum ProjectionMessage {
		None,
		CoreManagement,
		CoreProcessing,
		CoreStatus,
		EventReaderSubscription,
		FeedReader,
		Management,
		Misc,
		ReaderCoreService,
		ReaderSubscription,
		ReaderSubscriptionManagement,
		ServiceMessage,
		Subsystem,
	}
}
