namespace EventStore.Core.Services.RequestManager {
	public enum CommitLevel {
		Enqueued, //Write request enqued
		Leader, //Write on leader only
		Replicated, //Write on cluster quorum
		Indexed, //Indexed on Leader
		PerStream // stream prefix based
	}
}
