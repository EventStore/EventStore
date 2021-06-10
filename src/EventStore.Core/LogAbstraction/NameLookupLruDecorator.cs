using System.Collections.Generic;
using EventStore.Core.DataStructures;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.LogCommon;
using Value = System.UInt32;

namespace EventStore.Core.LogAbstraction {

	public class NameConfirmerLruDecorator : INameIndexConfirmer<Value> {
		private readonly ILRUCache<Value, string> _lru;
		private readonly INameIndexConfirmer<Value> _wrappedConfirmer;

		public NameConfirmerLruDecorator(ILRUCache<Value, string> lru, INameIndexConfirmer<Value> wrappedConfirmer) {
			_wrappedConfirmer = wrappedConfirmer;
			_lru = lru;
		}

		public void Confirm(
			IList<IPrepareLogRecord<Value>> prepares,
			bool catchingUp,
			IIndexBackend<Value> backend) {

			_wrappedConfirmer.Confirm(prepares, catchingUp, backend);

			//qq unideal that we are looking through all the prepares, and then again in the wrapped
			// we chould take advantage of knowing they are all for the same stream
			// but then, we are only checking an enum field for each one so it might be negligable
			for (int i = 0; i < prepares.Count; i++) {
				var prepare = prepares[i];

				if (prepare.RecordType == LogRecordType.Stream &&
					prepare is LogV3StreamRecord streamRecord) {

					_lru.Put(
						streamRecord.StreamNumber,
						streamRecord.StreamName);
				}
			}
		}

		public void Dispose() {
			_wrappedConfirmer.Dispose();
		}

		public void InitializeWithConfirmed(INameLookup<Value> source) {
			_wrappedConfirmer.InitializeWithConfirmed(source);
		}
	}

	// this guy speeds up name lookups by storing them in a LRU cache
	// it can intercept the confirmations too 
	public class NameLookupLruDecorator<TValue> : INameLookup<TValue> {
		private readonly ILRUCache<TValue, string> _lru;
		private readonly INameLookup<TValue> _wrappedLookup;

		public NameLookupLruDecorator(
			ILRUCache<TValue, string> lru,
			INameLookup<TValue> wrappedLookup) {
			_lru = lru;
			_wrappedLookup = wrappedLookup;
		}

		public bool TryGetLastValue(out TValue last) {
			return _wrappedLookup.TryGetLastValue(out last);
		}

		public bool TryGetName(TValue value, out string name) {
			if (_lru.TryGet(value, out name))
				return true;

			var res = _wrappedLookup.TryGetName(value, out name);

			if (res) {
				_lru.Put(value, name);
			}

			return res;
		}
	}
}
