namespace EventStore.Core.LogAbstraction {
	public class StreamNameLookupSingletonFactory<TStreamId> : IStreamNameLookupFactory<TStreamId> {
		private readonly IStreamNameLookup<TStreamId> _streamIdToName;

		public StreamNameLookupSingletonFactory(IStreamNameLookup<TStreamId> streamIdToName) {
			_streamIdToName = streamIdToName;
		}

		public IStreamNameLookup<TStreamId> Create(object input = null) {
			return _streamIdToName;
		}
	}
}
