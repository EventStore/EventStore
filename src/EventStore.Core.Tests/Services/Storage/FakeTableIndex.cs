using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Index;

namespace EventStore.Core.Tests.Services.Storage {
	public class FakeTableIndex<TStreamId> : ITableIndex<TStreamId> {
		internal static readonly IndexEntry InvalidIndexEntry = new IndexEntry(0, -1, -1);
		public int ScavengeCount { get; private set; }

		public long PrepareCheckpoint {
			get { throw new NotImplementedException(); }
		}

		public long CommitCheckpoint {
			get { throw new NotImplementedException(); }
		}

		public void Initialize(long chaserCheckpoint) {
		}

		public void Close(bool removeFiles = true) {
		}

		public void Add(long commitPos, TStreamId streamId, long version, long position) {
			throw new NotImplementedException();
		}

		public void AddEntries(long commitPos, IList<IndexKey<TStreamId>> entries) {
			throw new NotImplementedException();
		}

		public bool TryGetOneValue(TStreamId streamId, long version, out long position) {
			position = -1;
			return false;
		}

		public bool TryGetLatestEntry(TStreamId streamId, out IndexEntry entry) {
			entry = InvalidIndexEntry;
			return false;
		}

		public bool TryGetOldestEntry(TStreamId streamId, out IndexEntry entry) {
			entry = InvalidIndexEntry;
			return false;
		}

		public IReadOnlyList<IndexEntry> GetRange(TStreamId streamId, long startVersion, long endVersion,
			int? limit = null) {
			return Array.Empty<IndexEntry>();
		}

		public void Scavenge(IIndexScavengerLog log, CancellationToken ct) {
			ScavengeCount++;
		}

		public Task MergeIndexes() {
			return Task.CompletedTask;
		}

		public bool IsBackgroundTaskRunning {
			get { return false; }
		}
	}
}
