using System;
using System.Collections.Concurrent;
using EventStore.Common.Utils;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Services;

namespace EventStore.Core.LogV3 {

	// temporary implementation as stepping stone
	public class InMemoryStreamNameIndex :
		IStreamNameIndex<long>,
		IStreamIdLookup<long>,
		IStreamNameLookup<long> {

		public InMemoryStreamNameIndex() {
		}

		long _next = LogV3SystemStreams.FirstRealStream;
		readonly ConcurrentDictionary<string, long> _dict = new ConcurrentDictionary<string, long>();
		readonly ConcurrentDictionary<long, string> _rev = new ConcurrentDictionary<long, string>();

		public bool GetOrAddId(string name, out long streamNumber, out long createdId, out string createdName) {
			Ensure.NotNullOrEmpty(name, "name");
			if (SystemStreams.IsMetastream(name))
				throw new ArgumentException(nameof(name));

			var oldNext = _next;
			streamNumber = _dict.GetOrAdd(name, n => {
				_next += 2;
				var streamNumber = _next;
				_dict[n] = streamNumber;
				_rev[streamNumber] = n;
				return streamNumber;
			});

			// return true if we found an existing entry. i.e. did not have to allocate from _next
			createdId = streamNumber;
			createdName = name;
			return oldNext == _next;
		}

		public long LookupId(string streamName) {
			if (!_dict.TryGetValue(streamName, out var streamNumber))
				return 0;

			return streamNumber;
		}

		// todo: will be able to read from the index once we have stream records
		// (even in mem), at which point we can drop implementation of IStreamNameLookup here.
		public string LookupName(long streamNumber) {
			_rev.TryGetValue(streamNumber, out var name);
			return name ?? "";
		}
	}
}
