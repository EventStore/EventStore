using System;

namespace EventStore.Core.LogAbstraction {
	public interface INameExistenceFilter : IExistenceFilterReader<string>, IDisposable {
		void Initialize(INameEnumerator source);
		void Add(string name, long checkpoint);
	}

	public interface IExistenceFilterReader<T> {
		bool MightExist(T item);
	}
}
