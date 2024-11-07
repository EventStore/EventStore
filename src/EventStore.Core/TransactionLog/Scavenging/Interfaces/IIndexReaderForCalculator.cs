// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.TransactionLog.Scavenging;

public interface IIndexReaderForCalculator<TStreamId> {
	ValueTask<long> GetLastEventNumber(StreamHandle<TStreamId> streamHandle, ScavengePoint scavengePoint, CancellationToken token);

	ValueTask<IndexReadEventInfoResult> ReadEventInfoForward(
		StreamHandle<TStreamId> stream,
		long fromEventNumber,
		int maxCount,
		ScavengePoint scavengePoint,
		CancellationToken token);

	ValueTask<bool> IsTombstone(long logPosition, CancellationToken token);
}
