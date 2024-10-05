// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.TransactionLog.Scavenging;

public enum DiscardDecision {
	None,
	Discard,
	// calculator cannot tell conclusively, depends on record timestamp that it only has
	// approximately. chunks can tell accurately because they have the precise timestamp.
	// index will just keep such indexentries.
	MaybeDiscard,
	Keep,
	AlreadyDiscarded,
}
