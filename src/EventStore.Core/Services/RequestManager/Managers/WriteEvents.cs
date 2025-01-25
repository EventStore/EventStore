// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using static EventStore.Core.Messages.ClientMessage;
using static EventStore.Core.Messages.StorageMessage;

namespace EventStore.Core.Services.RequestManager.Managers;

public class WriteEvents(
	IPublisher publisher,
	TimeSpan timeout,
	IEnvelope clientResponseEnvelope,
	Guid internalCorrId,
	Guid clientCorrId,
	string streamId,
	long expectedVersion,
	Event[] events,
	CommitSource commitSource,
	CancellationToken cancellationToken = default)
	: RequestManagerBase(publisher,
		timeout,
		clientResponseEnvelope,
		internalCorrId,
		clientCorrId,
		expectedVersion,
		commitSource,
		prepareCount: 0,
		waitForCommit: true) {
	protected override Message WriteRequestMsg =>
		new WritePrepares(InternalCorrId, WriteReplyEnvelope, streamId, ExpectedVersion, events, cancellationToken);

	protected override Message ClientSuccessMsg =>
		 new WriteEventsCompleted(ClientCorrId, FirstEventNumber, LastEventNumber,
			 CommitPosition,  //not technically correct, but matches current behavior correctly
			 CommitPosition);

	protected override Message ClientFailMsg =>
		 new WriteEventsCompleted(ClientCorrId, Result, FailureMessage, FailureCurrentVersion);
}
