using System;
using System.Collections.Generic;

namespace EventStore.Core.Index
{
    public interface ISearchTable
    {
        Guid Id { get; }
        int Count { get; }

        bool TryGetOneValue(uint stream, int number, out long position);
        bool TryGetLatestEntry(uint stream, out IndexEntry entry);
        bool TryGetOldestEntry(uint stream, out IndexEntry entry);
        IEnumerable<IndexEntry> GetRange(uint stream, int startNumber, int endNumber);
        IEnumerable<IndexEntry> IterateAllInOrder();
    }
}