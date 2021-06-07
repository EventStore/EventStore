using System;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.LogAbstraction {
	// mechanism to delay construction of StreamNames and SystemStreams until the IndexReader is available
	public class AdHocStreamNamesProvider<TStreamId> : IStreamNamesProvider<TStreamId> {
		private readonly Func<IIndexReader<TStreamId>, (ISystemStreamLookup<TStreamId>, INameLookup<TStreamId>, INameEnumerator)> _setReader;
		ISystemStreamLookup<TStreamId> _systemStreams;
		INameLookup<TStreamId> _streamNames;
		INameEnumerator _streamNameEnumerator;

		public AdHocStreamNamesProvider(Func<IIndexReader<TStreamId>, (ISystemStreamLookup<TStreamId>, INameLookup<TStreamId>, INameEnumerator)> setReader) {
			_setReader = setReader;
		}

		public INameLookup<TStreamId> StreamNames =>
			_streamNames ?? throw new InvalidOperationException("Call SetReader first");

		public ISystemStreamLookup<TStreamId> SystemStreams =>
			_systemStreams ?? throw new InvalidOperationException("Call SetReader first");

		public INameEnumerator StreamNameEnumerator =>
			_streamNameEnumerator ?? throw new InvalidOperationException("Call SetReader first");

		public void SetReader(IIndexReader<TStreamId> reader) {
			(_systemStreams, _streamNames, _streamNameEnumerator) = _setReader(reader);
		}
	}
}
