// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.TransactionLog.Checkpoint {
	public static class Checkpoint {
		public const string Writer = "writer";
		public const string Chaser = "chaser";
		public const string Epoch = "epoch";
		public const string Proposal = "proposal";
		public const string Truncate = "truncate";
		public const string Replication = "replication";
		public const string Index = "index";
		public const string StreamExistenceFilter = "streamExistenceFilter";
	}
}
