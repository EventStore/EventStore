namespace EventStore.Core.Data {
	public enum ReadEventResult {
		Success = 0,
		NotFound = 1,
		NoStream = 2,
		StreamDeleted = 3,
		Error = 4,
		AccessDenied = 5
	}
}
