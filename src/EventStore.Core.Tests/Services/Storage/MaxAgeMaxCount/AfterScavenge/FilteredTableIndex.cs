using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Index;

namespace EventStore.Core.Tests.Services.Storage.MaxAgeMaxCount.AfterScavenge {
	// simulates particular entries having been removed from the index by scavenge
	public class FilteredTableIndex<TStreamId> : ITableIndex<TStreamId> {
		private readonly ITableIndex<TStreamId> _wrapped;
		private readonly Func<IndexEntry, bool> _condition;
		public FilteredTableIndex(ITableIndex<TStreamId> wrapped, Func<IndexEntry, bool> condition) {
			_wrapped = wrapped;
			_condition = condition;
		}

		public long CommitCheckpoint => _wrapped.CommitCheckpoint;

		public long PrepareCheckpoint => _wrapped.PrepareCheckpoint;

		public bool IsBackgroundTaskRunning => _wrapped.IsBackgroundTaskRunning;

		public void Add(long commitPos, TStreamId streamId, long version, long position) {
			_wrapped.Add(commitPos, streamId, version, position);
		}

		public void AddEntries(long commitPos, IList<IndexKey<TStreamId>> entries) {
			_wrapped.AddEntries(commitPos, entries);
		}

		public void Close(bool removeFiles = true) {
			_wrapped.Close(removeFiles);
		}

		public IReadOnlyList<IndexEntry> GetRange(TStreamId streamId, long startVersion, long endVersion, int? limit = null) {
			return _wrapped
				.GetRange(streamId, startVersion, endVersion, limit)
				.Where(_condition)
				.ToList();
		}

		public IReadOnlyList<IndexEntry> GetRange(ulong stream, long startVersion, long endVersion, int? limit = null) {
			return _wrapped
				.GetRange(stream, startVersion, endVersion, limit)
				.Where(_condition)
				.ToList();
		}

		public void Initialize(long chaserCheckpoint) {
			_wrapped.Initialize(chaserCheckpoint);
		}

		public IEnumerable<ISearchTable> IterateAllInOrder() {
			return _wrapped.IterateAllInOrder();
		}

		public Task MergeIndexes() {
			return _wrapped.MergeIndexes();
		}

		public void Scavenge(IIndexScavengerLog log, CancellationToken ct) {
			throw new NotImplementedException();
		}

		public void Scavenge(Func<IndexEntry, bool> shouldKeep, IIndexScavengerLog log, CancellationToken ct) {
			throw new NotImplementedException();
		}

		public bool TryGetLatestEntry(TStreamId streamId, out IndexEntry entry) {
			var got = _wrapped.TryGetLatestEntry(streamId, out entry);
			if (!got)
				return false;
			if (_condition(entry))
				return true;

			// we got the latest entry from the wrapped but it doesn't pass our condition
			var range = GetRange(streamId, 0, long.MaxValue);
			if (range.Count == 0)
				return false;
			entry = range[0];
			return true;
		}

		public bool TryGetLatestEntry(ulong stream, long beforePosition, Func<IndexEntry, bool> isForThisStream, out IndexEntry entry) {
			throw new NotImplementedException();
		}

		public bool TryGetLatestEntry(TStreamId streamId, long beforePosition, Func<IndexEntry, bool> isForThisStream, out IndexEntry entry) {
			throw new NotImplementedException();
		}

		public bool TryGetNextEntry(TStreamId streamId, long afterVersion, out IndexEntry entry) {
			var got = _wrapped.TryGetNextEntry(streamId, afterVersion, out entry);
			if (!got)
				return false;
			if (_condition(entry))
				return true;

			// we got the next entry from wrapped but it doesn't pass our condition
			var range = GetRange(streamId, afterVersion, long.MaxValue);
			if (range.Count == 0)
				return false;
			entry = range[^1];
			return true;
		}

		public bool TryGetNextEntry(ulong stream, long afterVersion, out IndexEntry entry) {
			var got = _wrapped.TryGetNextEntry(stream, afterVersion, out entry);
			if (!got)
				return false;
			if (_condition(entry))
				return true;

			// we got the next entry from wrapped but it doesn't pass our condition
			var range = GetRange(stream, afterVersion, long.MaxValue);
			if (range.Count == 0)
				return false;
			entry = range[^1];
			return true;
		}

		public bool TryGetOldestEntry(TStreamId streamId, out IndexEntry entry) {
			var got = _wrapped.TryGetOldestEntry(streamId, out entry);
			if (!got)
				return false;
			if (_condition(entry))
				return true;

			// we got the oldest entry from the wrapped but it doesn't pass our condition
			var range = GetRange(streamId, 0, long.MaxValue);
			if (range.Count == 0)
				return false;
			entry = range[^1];
			return true;
		}

		public bool TryGetOneValue(TStreamId streamId, long version, out long position) {
			throw new NotImplementedException();
		}

		public bool TryGetPreviousEntry(TStreamId streamId, long beforeVersion, out IndexEntry entry) {
			throw new NotImplementedException();
		}

		public bool TryGetPreviousEntry(ulong stream, long beforeVersion, out IndexEntry entry) {
			throw new NotImplementedException();
		}

		public void WaitForBackgroundTasks(int millisecondsTimeout) {
			_wrapped.WaitForBackgroundTasks(millisecondsTimeout);
		}
	}
}
