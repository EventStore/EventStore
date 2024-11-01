// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.TransactionLog.Scavenging;

public class IndexReaderForAccumulator<TStreamId> : IIndexReaderForAccumulator<TStreamId> {
	private readonly IReadIndex<TStreamId> _readIndex;

	public IndexReaderForAccumulator(IReadIndex<TStreamId> readIndex) {
		_readIndex = readIndex;
	}

	// reads a stream forward but only returns event info not the full event.
	public ValueTask<IndexReadEventInfoResult> ReadEventInfoForward(
		StreamHandle<TStreamId> handle,
		long fromEventNumber,
		int maxCount,
		ScavengePoint scavengePoint,
		CancellationToken token) {

		switch (handle.Kind) {
			case StreamHandle.Kind.Hash:
				// uses the index only
				return _readIndex.ReadEventInfoForward_NoCollisions(
					handle.StreamHash,
					fromEventNumber,
					maxCount,
					scavengePoint.Position,
					token);
			case StreamHandle.Kind.Id:
				// uses log to check for hash collisions
				return _readIndex.ReadEventInfoForward_KnownCollisions(
					handle.StreamId,
					fromEventNumber,
					maxCount,
					scavengePoint.Position,
					token);
			default:
				return ValueTask.FromException<IndexReadEventInfoResult>(new ArgumentOutOfRangeException(nameof(handle), handle, null));
		}
	}

	// reads a stream backward but only returns event info not the full event.
	public ValueTask<IndexReadEventInfoResult> ReadEventInfoBackward(
		TStreamId streamId,
		StreamHandle<TStreamId> handle,
		long fromEventNumber,
		int maxCount,
		ScavengePoint scavengePoint,
		CancellationToken token) {

		switch (handle.Kind) {
			case StreamHandle.Kind.Hash:
				// uses the index only
				return _readIndex.ReadEventInfoBackward_NoCollisions(
					handle.StreamHash,
					_ => streamId,
					fromEventNumber,
					maxCount,
					scavengePoint.Position,
					token);
			case StreamHandle.Kind.Id:
				// uses log to check for hash collisions
				return _readIndex.ReadEventInfoBackward_KnownCollisions(
					handle.StreamId,
					fromEventNumber,
					maxCount,
					scavengePoint.Position,
					token);
			default:
				return ValueTask.FromException<IndexReadEventInfoResult>(new ArgumentOutOfRangeException(nameof(handle), handle, null));
		}
	}
}
