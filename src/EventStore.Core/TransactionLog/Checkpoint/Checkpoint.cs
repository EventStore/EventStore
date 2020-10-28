namespace EventStore.Core.TransactionLog.Checkpoint {
	public static class Checkpoint {
		public const string Writer = "writer";
		public const string Chaser = "chaser";
		public const string Epoch = "epoch";
		public const string EpochStage = "epoch_stage";
		public const string EpochCommit = "epoch_commit";
		public const string Truncate = "truncate";
		public const string Replication = "replication";
		public const string Index = "index";
	}
}
