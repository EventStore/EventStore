using System.Collections.Generic;
using System.Linq;
using EventStore.Core.LogAbstraction;

namespace EventStore.Core.Tests.XUnit.LogV3 {
	class MockNameLookup : INameLookup<long> {
		private readonly Dictionary<long, string> _dict;

		public MockNameLookup(Dictionary<long, string> dict) {
			_dict = dict;
		}

		public bool TryGetLastValue(out long last) {
			last = _dict.Count != 0 ? _dict.Keys.Max() : 0;
			return _dict.Count != 0;
		}

		public bool TryGetName(long key, out string name) {
			return _dict.TryGetValue(key, out name);
		}
	}
}
