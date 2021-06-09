using System;
using EventStore.Core.Index;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.LogAbstraction {
	// mechanism to delay construction of StreamNames and SystemStreams until the IndexReader is available
	public class AdHocStreamNamesProvider<TStreamId> : IStreamNamesProvider<TStreamId> {
		private readonly Func<IIndexReader<TStreamId>, (ISystemStreamLookup<TStreamId>, INameLookup<TStreamId>, INameExistenceFilterInitializer)> _setReader;
		private Func<ITableIndex, INameExistenceFilterInitializer> _setTableIndex;

		private ISystemStreamLookup<TStreamId> _systemStreams;
		private INameLookup<TStreamId> _streamNames;
		private INameExistenceFilterInitializer _streamNameExistenceFilterInitializer;

		public AdHocStreamNamesProvider(Func<IIndexReader<TStreamId>, (ISystemStreamLookup<TStreamId>, INameLookup<TStreamId>, INameExistenceFilterInitializer)> setReader,
			Func<ITableIndex, INameExistenceFilterInitializer> setTableIndex,
			ISystemStreamLookup<TStreamId> systemStreams,
			INameLookup<TStreamId> streamNames,
			INameExistenceFilterInitializer streamNameExistenceFilterInitializer) {
			_setReader = setReader;
			_setTableIndex = setTableIndex;
			_systemStreams = systemStreams;
			_streamNames = streamNames;
			_streamNameExistenceFilterInitializer = streamNameExistenceFilterInitializer;
		}

		public INameLookup<TStreamId> StreamNames =>
			_streamNames ?? throw new InvalidOperationException("Call SetReader first");

		public ISystemStreamLookup<TStreamId> SystemStreams =>
			_systemStreams ?? throw new InvalidOperationException("Call SetReader first");

		public INameExistenceFilterInitializer StreamNameExistenceFilterInitializer =>
			_streamNameExistenceFilterInitializer ?? throw new InvalidOperationException("Call SetReader or SetTableIndex first");

		public void SetReader(IIndexReader<TStreamId> reader) {
			var (systemStreams, streamNames, streamNameExistenceFilterInitializer) = _setReader(reader);
			_systemStreams = systemStreams ?? _systemStreams;
			_streamNames = streamNames ?? _streamNames;
			_streamNameExistenceFilterInitializer = streamNameExistenceFilterInitializer ?? _streamNameExistenceFilterInitializer;
		}
		public void SetTableIndex(ITableIndex tableIndex) {
			var streamNameExistenceFilterInitializer = _setTableIndex(tableIndex);
			_streamNameExistenceFilterInitializer = streamNameExistenceFilterInitializer ?? _streamNameExistenceFilterInitializer;
		}
	}
}
