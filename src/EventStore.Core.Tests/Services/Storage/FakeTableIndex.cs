using System;
using System.Collections.Generic;
using EventStore.Core.Index;

namespace EventStore.Core.Tests.Services.Storage
{
    public class FakeTableIndex: ITableIndex
    {
        internal static readonly IndexEntry InvalidIndexEntry = new IndexEntry(0, -1, -1);

        public long PrepareCheckpoint { get { throw new NotImplementedException(); } }
        public long CommitCheckpoint { get { throw new NotImplementedException(); } }

        public void Initialize(long chaserCheckpoint)
        {
        }

        public void Close(bool removeFiles = true)
        {
        }

        public void Add(long commitPos, uint stream, int version, long position)
        {
            throw new NotImplementedException();
        }

        public void AddEntries(long commitPos, IList<IndexEntry> entries)
        {
            throw new NotImplementedException();
        }

        public bool TryGetOneValue(uint stream, int version, out long position)
        {
            position = -1;
            return false;
        }

        public bool TryGetLatestEntry(uint stream, out IndexEntry entry)
        {
            entry = InvalidIndexEntry;
            return false;
        }

        public bool TryGetOldestEntry(uint stream, out IndexEntry entry)
        {
            entry = InvalidIndexEntry;
            return false;
        }

        public IEnumerable<IndexEntry> GetRange(uint stream, int startVersion, int endVersion)
        {
            yield break;
        }
    }
}