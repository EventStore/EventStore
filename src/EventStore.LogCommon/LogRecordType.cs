namespace EventStore.LogCommon {
	public enum LogRecordType : byte {
		Prepare = 0,
		Commit = 1,
		System = 2,
		PartitionType = 3,
		StreamType = 4,
		LogV3StreamWrite = 5,
		Stream = 6,
	}
}
