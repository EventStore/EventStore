using System.Security.Claims;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Util;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public interface IReadIndex {
		long LastIndexedPosition { get; }

		ReadIndexStats GetStatistics();

		/// <summary>
		/// Returns event records in the sequence they were committed into TF.
		/// Positions is specified as pre-positions (pointer at the beginning of the record).
		/// </summary>
		IndexReadAllResult ReadAllEventsForward(TFPos pos, int maxCount);

		/// <summary>
		/// Returns event records in the reverse sequence they were committed into TF.
		/// Positions is specified as post-positions (pointer after the end of record).
		/// </summary>
		IndexReadAllResult ReadAllEventsBackward(TFPos pos, int maxCount);

		/// <summary>
		/// Returns event records whose eventType matches the given EventFilter in the sequence they were committed into TF.
		/// Positions is specified as pre-positions (pointer at the beginning of the record).
		/// </summary>
		IndexReadAllResult ReadAllEventsForwardFiltered(TFPos pos, int maxCount, int maxSearchWindow,
			IEventFilter eventFilter);

		/// <summary>
		/// Returns event records whose eventType matches the given EventFilter in the sequence they were committed into TF.
		/// Positions is specified as pre-positions (pointer at the beginning of the record).
		/// </summary>
		IndexReadAllResult ReadAllEventsBackwardFiltered(TFPos pos, int maxCount, int maxSearchWindow,
			IEventFilter eventFilter);

		void Close();
		void Dispose();
	}

	public interface IReadIndex<TStreamId> : IReadIndex {
		IIndexWriter<TStreamId> IndexWriter { get; }

		// streamId drives the read, streamName is only for populating on the result.
		// this was less messy than safely adding the streamName to the EventRecord at some point after construction
		IndexReadEventResult ReadEvent(string streamName, TStreamId streamId, long eventNumber);
		IndexReadStreamResult ReadStreamEventsBackward(string streamName, TStreamId streamId, long fromEventNumber, int maxCount);
		IndexReadStreamResult ReadStreamEventsForward(string streamName, TStreamId streamId, long fromEventNumber, int maxCount);

		bool IsStreamDeleted(TStreamId streamId);
		long GetStreamLastEventNumber(TStreamId streamId);
		StreamMetadata GetStreamMetadata(TStreamId streamId);
		StorageMessage.EffectiveAcl GetEffectiveAcl(TStreamId streamId);
		TStreamId GetEventStreamIdByTransactionId(long transactionId);

		TStreamId GetStreamId(string streamName);
		string GetStreamName(TStreamId streamId);
	}
}
