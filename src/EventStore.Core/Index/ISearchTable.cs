using System;
using System.Collections.Generic;

namespace EventStore.Core.Index
{
    public interface ISearchTable
    {
        Guid Id { get; }
        long Count { get; }
        byte Version { get; }

        bool TryGetOneValue(ulong stream, int number, out long position);
        bool TryGetLatestEntry(ulong stream, out IndexEntry entry);
        bool TryGetOldestEntry(ulong stream, out IndexEntry entry);
        IEnumerable<IndexEntry> GetRange(ulong stream, int startNumber, int endNumber, int? limit = null);
        IEnumerable<IndexEntry> IterateAllInOrder();
    }
}