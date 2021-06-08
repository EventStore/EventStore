using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.LogAbstraction {
	public class SingletonStreamNamesProvider<TStreamId> : IStreamNamesProvider<TStreamId> {
		public ISystemStreamLookup<TStreamId> SystemStreams { get; }

		public INameLookup<TStreamId> StreamNames { get; }

		public INameExistenceFilterInitializer StreamNameExistenceFilterInitializer { get; }

		public SingletonStreamNamesProvider(
			ISystemStreamLookup<TStreamId> systemStreams,
			INameLookup<TStreamId> streamNames,
			INameExistenceFilterInitializer streamNameExistenceFilterInitializer) {

			SystemStreams = systemStreams;
			StreamNames = streamNames;
			StreamNameExistenceFilterInitializer = streamNameExistenceFilterInitializer;
		}

		public void SetReader(IIndexReader<TStreamId> reader) {
		}
	}
}
