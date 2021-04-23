namespace EventStore.LogCommon {
	public enum LogRecordType : byte {
		Prepare = 0,
		Commit = 1,
		System = 2,
		PartitionType = 3,
		StreamType = 4,
		EventType = 5,
		ContentType = 6,
		Partition = 7,
		StreamWrite = 8,
	}
}
