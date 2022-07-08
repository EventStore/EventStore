namespace EventStore.Core.TransactionLog.Scavenging {
	public enum DiscardDecision {
		None,
		Discard,
		// calculator cannot tell conclusively, depends on record timestamp that it only has
		// approximately. chunks can tell accurately because they have the precise timestamp.
		// index will just keep such indexentries.
		MaybeDiscard,
		Keep,
	}
}
