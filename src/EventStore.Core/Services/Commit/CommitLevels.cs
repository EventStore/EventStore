namespace EventStore.Core.Services.Commit {
	public enum CommitLevel {
		ClusterWrite, //Writes are cluster committed
		MasterIndexed //Index on master is readable
	}
}
