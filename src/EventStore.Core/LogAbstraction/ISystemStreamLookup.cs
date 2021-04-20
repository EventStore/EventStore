namespace EventStore.Core.LogAbstraction {
	public interface ISystemStreamLookup<TStreamId> {
		TStreamId AllStream { get; }
		TStreamId SettingsStream { get; }

		bool IsMetaStream(TStreamId streamId);
		bool IsSystemStream(TStreamId streamId);
		TStreamId MetaStreamOf(TStreamId streamId);
		TStreamId OriginalStreamOf(TStreamId streamId);
	}
}
