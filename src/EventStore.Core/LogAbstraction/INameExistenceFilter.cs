using System;

namespace EventStore.Core.LogAbstraction {
	public interface INameExistenceFilter : IExistenceFilterReader<string>, IDisposable {
		void Initialize(INameExistenceFilterInitializer source);
		void Verify();
		void Add(string name);
		void Add(ulong hash);
		long CurrentCheckpoint { get; set; }
	}

	public interface IExistenceFilterReader<T> {
		bool MightContain(T item);
	}
}
