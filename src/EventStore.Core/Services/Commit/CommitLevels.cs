namespace EventStore.Core.Services.Commit {
	public enum CommitLevel {
		MasterWrite,//Write on Master
		ClusterWrite, //Write on Cluster Quorum
		MasterIndexed //Indexed on Master
	}
}
