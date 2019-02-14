namespace EventStore.Core.Data {
	public enum ReadStreamResult {
		Success = 0,
		NoStream = 1,
		StreamDeleted = 2,
		NotModified = 3,
		Error = 4,
		AccessDenied = 5
	}
}
