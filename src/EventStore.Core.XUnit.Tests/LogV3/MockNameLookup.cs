using System.Collections.Generic;
using System.Linq;
using EventStore.Core.LogAbstraction;
using StreamId = System.UInt32;

namespace EventStore.Core.XUnit.Tests.LogV3 {
	class MockNameLookup : INameLookup<StreamId> {
		private readonly Dictionary<StreamId, string> _dict;

		public MockNameLookup(Dictionary<StreamId, string> dict) {
			_dict = dict;
		}

		public bool TryGetLastValue(out StreamId last) {
			last = _dict.Count != 0 ? _dict.Keys.Max() : 0;
			return _dict.Count != 0;
		}

		public bool TryGetName(StreamId key, out string name) {
			return _dict.TryGetValue(key, out name);
		}
	}
}
