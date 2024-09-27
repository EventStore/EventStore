// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public struct CommitCheckResult<TStreamId> {
		public readonly CommitDecision Decision;
		public readonly TStreamId EventStreamId;
		public readonly long CurrentVersion;
		public readonly long StartEventNumber;
		public readonly long EndEventNumber;
		public readonly bool IsSoftDeleted;
		public readonly long IdempotentLogPosition;

		public CommitCheckResult(CommitDecision decision,
			TStreamId eventStreamId,
			long currentVersion,
			long startEventNumber,
			long endEventNumber,
			bool isSoftDeleted,
			long idempotentLogPosition = -1) {
			Decision = decision;
			EventStreamId = eventStreamId;
			CurrentVersion = currentVersion;
			StartEventNumber = startEventNumber;
			EndEventNumber = endEventNumber;
			IsSoftDeleted = isSoftDeleted;
			IdempotentLogPosition = idempotentLogPosition;
		}
	}
}
