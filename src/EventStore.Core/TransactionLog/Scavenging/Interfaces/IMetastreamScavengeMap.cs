namespace EventStore.Core.TransactionLog.Scavenging {
	public interface IMetastreamScavengeMap<TKey> :
		IScavengeMap<TKey, MetastreamData> {

		void SetTombstone(TKey key);

		void SetDiscardPoint(TKey key, DiscardPoint discardPoint);

		void DeleteAll();
	}
}
