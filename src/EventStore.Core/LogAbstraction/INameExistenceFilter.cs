using System;

namespace EventStore.Core.LogAbstraction {
	public interface INameExistenceFilter : IExistenceFilterReader<string>, IDisposable {
		void Initialize(INameExistenceFilterInitializer source);
		void Add(string name, long checkpoint);
		void Add(ulong hash, long checkpoint);
		long CurrentCheckpoint { get; }
	}

	public interface IExistenceFilterReader<T> {
		bool MightContain(T item);
	}
}
