using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Index;

namespace EventStore.Core.Tests.Services.Storage {
	public class FakeInMemoryTableIndex : ITableIndex
	{
		public long CommitCheckpoint => throw new NotImplementedException();

		public long PrepareCheckpoint => throw new NotImplementedException();

		public bool IsBackgroundTaskRunning => throw new NotImplementedException();

		private Dictionary<string, List<IndexKey> > _indexEntries = new Dictionary<string, List<IndexKey> >();
		public void Add(long commitPos, string streamId, long version, long position)
		{
			throw new NotImplementedException();
		}

		public void AddEntries(long commitPos, IList<IndexKey> entries)
		{
			foreach(var entry in entries){
				if(!_indexEntries.ContainsKey(entry.StreamId))
					_indexEntries[entry.StreamId] = new List<IndexKey>();
				_indexEntries[entry.StreamId].Add(entry);
			}
		}

		public void Close(bool removeFiles = true)
		{
		}

		public IEnumerable<IndexEntry> GetRange(string streamId, long startVersion, long endVersion, int? limit = null)
		{
			var entries = new List<IndexEntry>();
			if(_indexEntries.ContainsKey(streamId)){
				foreach(var entry in _indexEntries[streamId]){
					if(startVersion <= entry.Version && entry.Version <= endVersion)
						entries.Add(new IndexEntry(entry.Hash, entry.Version, entry.Position));
				}
			}
			return entries;
		}

		public void Initialize(long chaserCheckpoint)
		{
		}

		public Task MergeIndexes()
		{
			throw new NotImplementedException();
		}

		public void Scavenge(IIndexScavengerLog log, CancellationToken ct)
		{
			throw new NotImplementedException();
		}

		public bool TryGetLatestEntry(string streamId, out IndexEntry entry)
		{
			if(_indexEntries.ContainsKey(streamId)){
				var entries = _indexEntries[streamId];
				var lastEntry = entries[entries.Count - 1];
				entry = new IndexEntry(lastEntry.Hash, lastEntry.Version, lastEntry.Position);
				return true;
			}
			else{
				entry = new IndexEntry();
				return false;
			}
		}

		public bool TryGetOldestEntry(string streamId, out IndexEntry entry)
		{
			throw new NotImplementedException();
		}

		public bool TryGetOneValue(string streamId, long version, out long position)
		{
			throw new NotImplementedException();
		}
	}
}
