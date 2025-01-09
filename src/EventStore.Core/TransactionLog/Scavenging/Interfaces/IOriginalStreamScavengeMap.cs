// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using EventStore.Core.Data;

namespace EventStore.Core.TransactionLog.Scavenging.Interfaces;

public interface IOriginalStreamScavengeMap<TKey> :
	IScavengeMap<TKey, OriginalStreamData> {

	IEnumerable<KeyValuePair<TKey, OriginalStreamData>> ActiveRecords();

	IEnumerable<KeyValuePair<TKey, OriginalStreamData>> ActiveRecordsFromCheckpoint(TKey checkpoint);

	void SetTombstone(TKey key);

	void SetMetadata(TKey key, StreamMetadata metadata);

	void SetDiscardPoints(
		TKey key,
		CalculationStatus status,
		DiscardPoint discardPoint,
		DiscardPoint maybeDiscardPoint);

	bool TryGetChunkExecutionInfo(TKey key, out ChunkExecutionInfo info);

	void DeleteMany(bool deleteArchived);
}
