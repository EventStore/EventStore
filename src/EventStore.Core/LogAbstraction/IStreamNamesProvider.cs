using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.LogAbstraction {
	// certain abstraction points cant be provided until we have access to the index reader.
	// hopefully when some other pieces have fallen into place we can replace this with a nicer mechanism.
	public interface IStreamNamesProvider<TStreamId> {
		void SetReader(IIndexReader<TStreamId> reader);
		ISystemStreamLookup<TStreamId> SystemStreams { get; }
		INameLookup<TStreamId> StreamNames { get; }
	}
}
