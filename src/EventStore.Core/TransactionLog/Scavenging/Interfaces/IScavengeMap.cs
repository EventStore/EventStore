using System.Collections.Generic;

namespace EventStore.Core.TransactionLog.Scavenging {
	public interface IScavengeMap<TKey, TValue> {
		bool TryGetValue(TKey key, out TValue value);
		TValue this[TKey key] { set; }
		bool TryRemove(TKey key, out TValue value);
		IEnumerable<KeyValuePair<TKey, TValue>> AllRecords();
	}
}
