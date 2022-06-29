using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Index;

namespace EventStore.Core.Tests.Services.Storage {
	public class FakeTableIndex : ITableIndex {
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

		public void Add(long commitPos, string streamId, long version, long position) {
			throw new NotImplementedException();
		}

		public void AddEntries(long commitPos, IList<IndexKey> entries) {
			throw new NotImplementedException();
		}

		public bool TryGetOneValue(string streamId, long version, out long position) {
			position = -1;
			return false;
		}

		public bool TryGetLatestEntry(string streamId, out IndexEntry entry) {
			entry = InvalidIndexEntry;
			return false;
		}

		public bool TryGetLatestEntry(ulong stream, long beforePosition, Func<IndexEntry, bool> isForThisStream, out IndexEntry entry) {
			throw new NotImplementedException();
		}

		public bool TryGetLatestEntry(string streamId, long beforePosition, Func<IndexEntry, bool> isForThisStream, out IndexEntry entry) {
			throw new NotImplementedException();
		}

		public bool TryGetOldestEntry(string streamId, out IndexEntry entry) {
			entry = InvalidIndexEntry;
			return false;
		}

		public bool TryGetNextEntry(string streamId, long afterVersion, out IndexEntry entry) {
			throw new NotImplementedException();
		}

		public bool TryGetNextEntry(ulong stream, long afterVersion, out IndexEntry entry) {
			throw new NotImplementedException();
		}

		public bool TryGetPreviousEntry(string streamId, long beforeVersion, out IndexEntry entry) {
			throw new NotImplementedException();
		}

		public bool TryGetPreviousEntry(ulong stream, long beforeVersion, out IndexEntry entry) {
			throw new NotImplementedException();
		}

		public IEnumerable<IndexEntry> GetRange(string streamId, long startVersion, long endVersion,
			int? limit = null) {
			yield break;
		}

		public IEnumerable<IndexEntry> GetRange(ulong stream, long startVersion, long endVersion, int? limit = null) {
			throw new NotImplementedException();
		}

		public void Scavenge(IIndexScavengerLog log, CancellationToken ct) {
			ScavengeCount++;
		}

		public void Scavenge(
			Action<PTable> checkSuitability,
			Func<IndexEntry, bool> shouldKeep,
			IIndexScavengerLog log,
			CancellationToken ct) {

			Scavenge(log, ct);
		}

		public Task MergeIndexes() {
			return Task.CompletedTask;
		}

		public bool IsBackgroundTaskRunning {
			get { return false; }
		}
	}
}
