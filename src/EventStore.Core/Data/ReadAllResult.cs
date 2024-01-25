namespace EventStore.Core.Data {
	public enum ReadAllResult {
		Success = 0,
		NotModified = 1,
		Error = 2,
		AccessDenied = 3,
		Expired = 4,
		InvalidPosition = 5
	}
}
