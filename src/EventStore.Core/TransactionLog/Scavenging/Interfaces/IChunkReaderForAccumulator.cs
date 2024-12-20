// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Threading;

namespace EventStore.Core.TransactionLog.Scavenging.Interfaces;

public interface IChunkReaderForAccumulator<TStreamId> {
	// Each element in the enumerable indicates which of the three records has been populated for
	// that iteration.
	IAsyncEnumerable<AccumulatorRecordType> ReadChunkInto(
		int logicalChunkNumber,
		RecordForAccumulator<TStreamId>.OriginalStreamRecord originalStreamRecord,
		RecordForAccumulator<TStreamId>.MetadataStreamRecord metadataStreamRecord,
		RecordForAccumulator<TStreamId>.TombStoneRecord tombStoneRecord,
		CancellationToken token);
}
