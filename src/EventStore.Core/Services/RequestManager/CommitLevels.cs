namespace EventStore.Core.Services.RequestManager {
	public enum CommitLevel {
		Written, //Write on master
		Replicated, //Write on Cluster Quorum
		Indexed, //Indexed on Leader
		PerStream // stream prefix based
	}
}
