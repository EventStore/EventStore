// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DotNext;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.LogV2;

public class LogV2StreamNameIndex(INameExistenceFilter existenceFilter) :
	INameIndex<string>,
	INameIndexConfirmer<string>,
	IValueLookup<string>,
	INameLookup<string> {
	public void Dispose() {
	}

	public ValueTask InitializeWithConfirmed(INameLookup<string> source, CancellationToken token)
		=> token.IsCancellationRequested ? ValueTask.FromCanceled(token) : ValueTask.CompletedTask;

	public void CancelReservations() {
	}

	public void Confirm(IList<IPrepareLogRecord<string>> prepares, bool catchingUp, IIndexBackend<string> backend) {
		if (catchingUp) {
			// after the main index is caught up we will initialize the stream existence filter
			return;
		}

		if (prepares.Count == 0)
			return;

		var lastPrepare = prepares[^1];

		if (prepares[0].ExpectedVersion == ExpectedVersion.NoStream) {
			existenceFilter.Add(lastPrepare.EventStreamId);
		}

		existenceFilter.CurrentCheckpoint = lastPrepare.LogPosition;
	}

	// must use the commit to see if these are the first events in the stream
	// and for checkpointing.
	public void Confirm(
		IList<IPrepareLogRecord<string>> prepares,
		CommitLogRecord commit,
		bool catchingUp,
		IIndexBackend<string> backend) {

		if (catchingUp) {
			// after the main index is caught up we will initialize the stream existence filter
			return;
		}

		if (prepares.Count != 0 && commit.FirstEventNumber == 0) {
			var lastPrepare = prepares[^1];
			existenceFilter.Add(lastPrepare.EventStreamId);
		}

		existenceFilter.CurrentCheckpoint = commit.LogPosition;
	}

	public bool GetOrReserve(string streamName, out string streamId, out string createdId, out string createdName) {
		Debug.Assert(!string.IsNullOrWhiteSpace(streamName));
		streamId = streamName;
		createdId = null;
		createdName = null;

		// not adding the stream to the filter here, but this is safe because returning
		// true indicates that the stream might exist and no shortcut may be taken.
		return true;
	}

	public string LookupValue(string streamName) => streamName;

	public ValueTask<string> LookupName(string value, CancellationToken token)
		=> token.IsCancellationRequested ? ValueTask.FromCanceled<string>(token) : ValueTask.FromResult(value);

	public ValueTask<Optional<string>> TryGetLastValue(CancellationToken token)
		=> ValueTask.FromException<Optional<string>>(new System.NotImplementedException());
}
