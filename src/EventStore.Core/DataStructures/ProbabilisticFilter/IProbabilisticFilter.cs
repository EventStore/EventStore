using System;

namespace EventStore.Core.DataStructures.ProbabilisticFilter {
	public interface IProbabilisticFilter<in TItem> {
		void Add(TItem item);
		bool MightContain(TItem item);
	}

	public interface IProbabilisticFilter {
		void Add(ReadOnlySpan<byte> item);
		bool MightContain(ReadOnlySpan<byte> item);
	}
}
