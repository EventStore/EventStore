using System.Collections.Generic;
using EventStore.Core.Data;

namespace EventStore.Core.TransactionLog.Scavenging {
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
}
