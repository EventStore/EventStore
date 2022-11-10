using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.Index {
	public interface ITableIndex {
		long CommitCheckpoint { get; }
		long PrepareCheckpoint { get; }

		void Initialize(long chaserCheckpoint);
		void Close(bool removeFiles = true);
		void Scavenge(IIndexScavengerLog log, CancellationToken ct);
		Task MergeIndexes();
		IEnumerable<ISearchTable> IterateAllInOrder();
		bool IsBackgroundTaskRunning { get; }
	}

	public interface ITableIndex<TStreamId> : ITableIndex {
		void Add(long commitPos, TStreamId streamId, long version, long position);
		void AddEntries(long commitPos, IList<IndexKey<TStreamId>> entries);

		bool TryGetOneValue(TStreamId streamId, long version, out long position);
		bool TryGetLatestEntry(TStreamId streamId, out IndexEntry entry);
		bool TryGetOldestEntry(TStreamId streamId, out IndexEntry entry);
		bool TryGetNextEntry(TStreamId streamId, long afterVersion, out IndexEntry entry);
		bool TryGetNextEntry(ulong stream, long afterVersion, out IndexEntry entry);

		IReadOnlyList<IndexEntry> GetRange(TStreamId streamId, long startVersion, long endVersion, int? limit = null);

		void WaitForBackgroundTasks(int millisecondsTimeout);
	}
}
