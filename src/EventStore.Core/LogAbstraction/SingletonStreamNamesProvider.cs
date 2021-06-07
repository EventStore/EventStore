using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.LogAbstraction {
	public class SingletonStreamNamesProvider<TStreamId> : IStreamNamesProvider<TStreamId> {
		public ISystemStreamLookup<TStreamId> SystemStreams { get; }

		public INameLookup<TStreamId> StreamNames { get; }

		public INameEnumerator StreamNameEnumerator { get; }

		public SingletonStreamNamesProvider(
			ISystemStreamLookup<TStreamId> systemStreams,
			INameLookup<TStreamId> streamNames,
			INameEnumerator streamNameEnumerator) {

			SystemStreams = systemStreams;
			StreamNames = streamNames;
			StreamNameEnumerator = streamNameEnumerator;
		}

		public void SetReader(IIndexReader<TStreamId> reader) {
		}
	}
}
