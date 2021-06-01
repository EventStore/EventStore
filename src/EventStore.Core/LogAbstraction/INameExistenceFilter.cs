using System;

namespace EventStore.Core.LogAbstraction {
	public interface INameExistenceFilter<TValue> : IDisposable {
		void InitializeWithExisting(INameLookup<TValue> source);
		void Add(string name, TValue value);
		bool? Exists(string name);
	}
}
