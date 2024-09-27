namespace EventStore.Core.LogAbstraction {
	public interface ISystemStreamLookup<TStreamId> : IMetastreamLookup<TStreamId> {
		TStreamId AllStream { get; }
		TStreamId SettingsStream { get; }
		bool IsSystemStream(TStreamId streamId);
	}
}
