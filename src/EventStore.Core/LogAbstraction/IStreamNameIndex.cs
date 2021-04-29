namespace EventStore.Core.LogAbstraction {
	public interface IStreamNameIndex<TStreamId> {
		// return true => stream already existed.
		// return false => stream was created. createdId and createdName are the details of the created stream.
		// these can be different to streamName/streamId e.g. if streamName is a metastream.
		bool GetOrAddId(string streamName, out TStreamId streamId, out TStreamId createdId, out string createdName);
	}
}
