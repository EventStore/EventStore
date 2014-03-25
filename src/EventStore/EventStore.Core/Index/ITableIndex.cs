using System.Collections.Generic;

namespace EventStore.Core.Index
{
    public interface ITableIndex
    {
        long CommitCheckpoint { get; }
        long PrepareCheckpoint { get; }

        void Initialize(long chaserCheckpoint);
        void Close(bool removeFiles = true);

        void Add(long commitPos, uint stream, int version, long position);
        void AddEntries(long commitPos, IList<IndexEntry> entries);
        
        bool TryGetOneValue(uint stream, int version, out long position);
        bool TryGetLatestEntry(uint stream, out IndexEntry entry);
        bool TryGetOldestEntry(uint stream, out IndexEntry entry);

        IEnumerable<IndexEntry> GetRange(uint stream, int startVersion, int endVersion);
    }
}