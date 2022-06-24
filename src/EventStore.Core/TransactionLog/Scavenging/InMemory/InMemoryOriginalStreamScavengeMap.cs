using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;

namespace EventStore.Core.TransactionLog.Scavenging {
	public class InMemoryOriginalStreamScavengeMap<TKey> :
		InMemoryScavengeMap<TKey, OriginalStreamData>,
		IOriginalStreamScavengeMap<TKey> {

		public void SetTombstone(TKey key) {
			if (!TryGetValue(key, out var x))
				x = new OriginalStreamData();

			this[key] = new OriginalStreamData {
				DiscardPoint = x.DiscardPoint,
				MaybeDiscardPoint = x.MaybeDiscardPoint,
				MaxAge = x.MaxAge,
				MaxCount = x.MaxCount,
				TruncateBefore = x.TruncateBefore,

				// sqlite implementation would insert the record or update these columns
				Status = CalculationStatus.Active,
				IsTombstoned = true,
			};
		}

		public void SetMetadata(TKey key, StreamMetadata metadata) {
			if (!TryGetValue(key, out var x))
				x = new OriginalStreamData();

			this[key] = new OriginalStreamData {
				MaybeDiscardPoint = x.MaybeDiscardPoint,
				DiscardPoint = x.DiscardPoint,
				IsTombstoned = x.IsTombstoned,

				// sqlite implementation would insert the record or update these columns
				Status = CalculationStatus.Active,
				MaxAge = metadata.MaxAge,
				MaxCount = metadata.MaxCount,
				TruncateBefore = metadata.TruncateBefore,
			};
		}

		public void SetDiscardPoints(
			TKey key,
			CalculationStatus status,
			DiscardPoint discardPoint,
			DiscardPoint maybeDiscardPoint) {

			if (!TryGetValue(key, out var x))
				throw new Exception("this shouldn't happen");

			this[key] = new OriginalStreamData {
				IsTombstoned = x.IsTombstoned,
				MaxAge = x.MaxAge,
				MaxCount = x.MaxCount,
				TruncateBefore = x.TruncateBefore,

				// sqlite implementation would insert the record or update these columns
				Status = status,
				DiscardPoint = discardPoint,
				MaybeDiscardPoint = maybeDiscardPoint,
			};
		}

		public bool TryGetChunkExecutionInfo(TKey key, out ChunkExecutionInfo info) {
			if (!TryGetValue(key, out var data)) {
				info = default;
				return false;
			}

			// sqlite implementation would just select these columns
			info = new ChunkExecutionInfo(
				isTombstoned: data.IsTombstoned,
				discardPoint: data.DiscardPoint,
				maybeDiscardPoint: data.MaybeDiscardPoint,
				maxAge: data.MaxAge);

			return true;
		}

		private bool Filter(KeyValuePair<TKey, OriginalStreamData> kvp) =>
			kvp.Value.Status == CalculationStatus.Active;

		public IEnumerable<KeyValuePair<TKey, OriginalStreamData>> ActiveRecords() =>
			AllRecords().Where(Filter);

		public IEnumerable<KeyValuePair<TKey, OriginalStreamData>> ActiveRecordsFromCheckpoint(TKey checkpoint) =>
			// skip those which are before or equal to the checkpoint.
			ActiveRecords().SkipWhile(x => Comparer<TKey>.Default.Compare(x.Key, checkpoint) <= 0);

		public void DeleteMany(bool deleteArchived) {
			foreach (var kvp in AllRecords()) {
				if ((kvp.Value.Status == CalculationStatus.Spent) ||
					(kvp.Value.Status == CalculationStatus.Archived && deleteArchived)) {

					TryRemove(kvp.Key, out _);
				}
			}
		}
	}
}
