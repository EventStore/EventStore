// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.TransactionLog.Scavenging.Interfaces;

namespace EventStore.Core.TransactionLog.Scavenging;

public static class ScavengeStateExtensions {
	public static void SetCheckpoint(
		this IScavengeStateCommon state,
		ScavengeCheckpoint checkpoint) {

		var transaction = state.BeginTransaction();
		try {
			transaction.Commit(checkpoint);
		} catch {
			transaction.Rollback();
			throw;
		}
	}
}
