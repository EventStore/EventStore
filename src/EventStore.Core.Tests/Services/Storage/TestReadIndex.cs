using EventStore.Core.Data;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.Tests.Services {
	//interface that wraps an IReadIndex and IStreamNameIndex to be able to create stream IDs on the fly if they do
	//not exist yet mainly to facilitate conversion of existing LogV2 tests.
	public interface ITestReadIndex<TStreamId> : IReadIndex<TStreamId>, IStreamNameIndex<TStreamId> {
		public IndexReadEventResult ReadEvent(string streamName, long eventNumber);
		public IndexReadStreamResult ReadStreamEventsBackward(string streamName, long fromEventNumber, int maxCount);
		public IndexReadStreamResult ReadStreamEventsForward(string streamName, long fromEventNumber, int maxCount);
		public bool IsStreamDeleted(string streamName);
		public long GetStreamLastEventNumber(string streamName);
		public StreamMetadata GetStreamMetadata(string streamName);
		public StorageMessage.EffectiveAcl GetEffectiveAcl(string streamName);
		public string GetStreamName(string streamName);
	}

	public class TestReadIndex<TStreamId> : ITestReadIndex<TStreamId> {
		private IReadIndex<TStreamId> _readIndex;
		private IStreamNameIndex<TStreamId> _streamNameIndex;

		public TestReadIndex(IReadIndex<TStreamId> readIndex,
			IStreamNameIndex<TStreamId> streamNameIndex) {
			_readIndex = readIndex;
			_streamNameIndex = streamNameIndex;
		}
		public long LastIndexedPosition => _readIndex.LastIndexedPosition;
		public ReadIndexStats GetStatistics() => _readIndex.GetStatistics();
		public IndexReadAllResult ReadAllEventsForward(TFPos pos, int maxCount) => _readIndex.ReadAllEventsForward(pos, maxCount);
		public IndexReadAllResult ReadAllEventsBackward(TFPos pos, int maxCount) => _readIndex.ReadAllEventsBackward(pos, maxCount);
		public IndexReadAllResult ReadAllEventsForwardFiltered(TFPos pos, int maxCount, int maxSearchWindow, IEventFilter eventFilter) =>
			_readIndex.ReadAllEventsForwardFiltered(pos, maxCount, maxSearchWindow, eventFilter);
		public IndexReadAllResult ReadAllEventsBackwardFiltered(TFPos pos, int maxCount, int maxSearchWindow, IEventFilter eventFilter) =>
			_readIndex.ReadAllEventsBackwardFiltered(pos, maxCount, maxSearchWindow, eventFilter);
		public void Close() => _readIndex.Close();
		public void Dispose() => _readIndex.Dispose();
		public IIndexWriter<TStreamId> IndexWriter => _readIndex.IndexWriter;
		public IndexReadEventResult ReadEvent(string streamName, TStreamId streamId, long eventNumber) =>
			_readIndex.ReadEvent(streamName, streamId, eventNumber);
		public IndexReadStreamResult ReadStreamEventsBackward(string streamName, TStreamId streamId, long fromEventNumber, int maxCount) =>
			_readIndex.ReadStreamEventsBackward(streamName, streamId, fromEventNumber, maxCount);
		public IndexReadStreamResult
			ReadStreamEventsForward(string streamName, TStreamId streamId, long fromEventNumber, int maxCount) =>
			_readIndex.ReadStreamEventsForward(streamName, streamId, fromEventNumber, maxCount);
		public bool IsStreamDeleted(TStreamId streamId) => _readIndex.IsStreamDeleted(streamId);
			public long GetStreamLastEventNumber(TStreamId streamId) => _readIndex.GetStreamLastEventNumber(streamId);
			public StreamMetadata GetStreamMetadata(TStreamId streamId) => _readIndex.GetStreamMetadata(streamId);
		public StorageMessage.EffectiveAcl GetEffectiveAcl(TStreamId streamId) => _readIndex.GetEffectiveAcl(streamId);
		public TStreamId GetEventStreamIdByTransactionId(long transactionId) =>
			_readIndex.GetEventStreamIdByTransactionId(transactionId);
		public TStreamId GetStreamId(string streamName) => _readIndex.GetStreamId(streamName);
		public string GetStreamName(TStreamId streamId) => _readIndex.GetStreamName(streamId);
		public bool GetOrAddId(string streamName, out TStreamId streamId) =>
			_streamNameIndex.GetOrAddId(streamName, out streamId);

		private TStreamId GetOrAddStreamId(string streamName) {
			_streamNameIndex.GetOrAddId(streamName, out var streamId);
			return streamId;
		}
		public IndexReadEventResult ReadEvent(string streamName, long eventNumber) =>
			_readIndex.ReadEvent(streamName, GetOrAddStreamId(streamName), eventNumber);
		public IndexReadStreamResult ReadStreamEventsBackward(string streamName, long fromEventNumber, int maxCount) =>
			_readIndex.ReadStreamEventsBackward(streamName, GetOrAddStreamId(streamName), fromEventNumber, maxCount);
		public IndexReadStreamResult
			ReadStreamEventsForward(string streamName, long fromEventNumber, int maxCount) =>
			_readIndex.ReadStreamEventsForward(streamName, GetOrAddStreamId(streamName), fromEventNumber, maxCount);
		public bool IsStreamDeleted(string streamName) => _readIndex.IsStreamDeleted(GetOrAddStreamId(streamName));
		public long GetStreamLastEventNumber(string streamName) => _readIndex.GetStreamLastEventNumber(GetOrAddStreamId(streamName));
		public StreamMetadata GetStreamMetadata(string streamName) => _readIndex.GetStreamMetadata(GetOrAddStreamId(streamName));
		public StorageMessage.EffectiveAcl GetEffectiveAcl(string streamName) => _readIndex.GetEffectiveAcl(GetOrAddStreamId(streamName));
		public string GetStreamName(string streamName) => _readIndex.GetStreamName(GetOrAddStreamId(streamName));
	}
}
