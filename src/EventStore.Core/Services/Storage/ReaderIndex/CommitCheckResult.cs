// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

namespace EventStore.Core.Services.Storage.ReaderIndex;

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
