using EventStore.Core.DataStructures;

namespace EventStore.Core.LogAbstraction {

	public class NameConfirmerLruDecorator<TValue> : INameIndexConfirmer<TValue> {
		private readonly ILRUCache<TValue, string> _lru;
		private readonly INameIndexConfirmer<TValue> _wrappedConfirmer;

		public NameConfirmerLruDecorator(ILRUCache<TValue, string> lru, INameIndexConfirmer<TValue> wrappedConfirmer) {
			_wrappedConfirmer = wrappedConfirmer;
			_lru = lru;
		}

		public void Confirm(string name, TValue value) {
			_wrappedConfirmer.Confirm(name, value);
			_lru.Put(value, name);
		}

		public void Dispose() {
			_wrappedConfirmer.Dispose();
		}

		public void InitializeWithConfirmed(INameLookup<TValue> source) {
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
