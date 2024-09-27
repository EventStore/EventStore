namespace EventStore.Core.TransactionLog.LogRecords {
	public interface ISystemLogRecord : ILogRecord {
		SystemRecordType SystemRecordType { get; }
		EpochRecord GetEpochRecord();
	}
}
