using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.LogAbstraction {
	public class SingletonStreamNamesProvider<TStreamId> : IStreamNamesProvider<TStreamId> {
		public ISystemStreamLookup<TStreamId> SystemStreams { get; }

		public IStreamNameLookup<TStreamId> StreamNames { get; }

		public SingletonStreamNamesProvider(
			ISystemStreamLookup<TStreamId> systemStreams,
			IStreamNameLookup<TStreamId> streamNames) {

			SystemStreams = systemStreams;
			StreamNames = streamNames;
		}

		public void SetReader(IIndexReader<TStreamId> reader) {
		}
	}
}
