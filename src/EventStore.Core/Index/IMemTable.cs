using System.Collections.Generic;

namespace EventStore.Core.Index {
	public interface IMemTable : ISearchTable {
		bool MarkForConversion();
		void Add(ulong stream, long version, long position);
		void AddEntries(IList<IndexEntry> entries);
	}
}
