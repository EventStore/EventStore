// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.TransactionLog.Scavenging {
	public enum CalculationStatus {
		// Invalid
		None = 0,

		// Needs to be processed by calculator next time
		Active = 1,

		// Can be skipped over by calculator next time
		Archived = 2,

		// Can be deleted by cleaner when there are no chunks pending execution
		Spent = 3,
	}
}
