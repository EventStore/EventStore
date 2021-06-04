using System;

namespace EventStore.Core.LogAbstraction {
	public interface INameExistenceFilter<TCheckpoint> : IDisposable {
		void Initialize(INameEnumerator<TCheckpoint> source);
		void Add(string name, TCheckpoint checkpoint);
		bool? Exists(string name);
	}
}
