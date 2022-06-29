using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.Index {
	public interface ITableIndex {
		long CommitCheckpoint { get; }
		long PrepareCheckpoint { get; }

		void Initialize(long chaserCheckpoint);
		void Close(bool removeFiles = true);

		void Add(long commitPos, string streamId, long version, long position);
		void AddEntries(long commitPos, IList<IndexKey> entries);

		bool TryGetOneValue(string streamId, long version, out long position);
		bool TryGetLatestEntry(string streamId, out IndexEntry entry);
		bool TryGetLatestEntry(ulong stream, long beforePosition, Func<IndexEntry, bool> isForThisStream, out IndexEntry entry);
		bool TryGetLatestEntry(string streamId, long beforePosition, Func<IndexEntry, bool> isForThisStream, out IndexEntry entry);
		bool TryGetOldestEntry(string streamId, out IndexEntry entry);
		bool TryGetNextEntry(string streamId, long afterVersion, out IndexEntry entry);
		bool TryGetNextEntry(ulong stream, long afterVersion, out IndexEntry entry);
		bool TryGetPreviousEntry(string streamId, long beforeVersion, out IndexEntry entry);
		bool TryGetPreviousEntry(ulong stream, long beforeVersion, out IndexEntry entry);

		IEnumerable<IndexEntry> GetRange(string streamId, long startVersion, long endVersion, int? limit = null);
		IEnumerable<IndexEntry> GetRange(ulong stream, long startVersion, long endVersion, int? limit = null);

		// this overload keeps IndexEntries that exist in the log
		void Scavenge(IIndexScavengerLog log, CancellationToken ct);
		// this overload keeps IndexEntries that pass the keep predicate
		void Scavenge(Action<PTable> checkSuitability, Func<IndexEntry, bool> shouldKeep, IIndexScavengerLog log, CancellationToken ct);
		Task MergeIndexes();
		bool IsBackgroundTaskRunning { get; }
	}
}
