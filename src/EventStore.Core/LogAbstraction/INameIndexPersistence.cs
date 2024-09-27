using System;

namespace EventStore.Core.LogAbstraction {
	public interface INameIndexPersistence<TValue> : IValueLookup<TValue>, IDisposable {
		TValue LastValueAdded { get; }
		void Init(INameLookup<TValue> source);
		bool TryGetValue(string name, out TValue value);
		void Add(string name, TValue value);
	}
}
