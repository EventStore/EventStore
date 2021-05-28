using System;
using System.Collections.Concurrent;
using EventStore.Core.LogAbstraction;

namespace EventStore.Core.LogV3 {
	public class NameIndexInMemoryPersistence :
		INameIndexPersistence<long> {

		readonly ConcurrentDictionary<string, long> _dict = new();

		public long LastValueAdded { get; private set; }

		public NameIndexInMemoryPersistence() {
		}

		public void Dispose() {
		}

		public void Init(INameLookup<long> source) {
		}

		public void Add(string name, long value) {
			_dict[name] = value;
			LastValueAdded = value;
		}

		public bool TryGetValue(string name, out long value) =>
			_dict.TryGetValue(name, out value);

		public long LookupValue(string name) {
			if (string.IsNullOrEmpty(name))
				throw new ArgumentNullException(nameof(name));

			if (!_dict.TryGetValue(name, out var value))
				return 0;

			return value;
		}
	}
}
