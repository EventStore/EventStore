using System.Security.Claims;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLogV2.Data;
using EventStore.Core.Util;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public interface IReadIndex {
		long LastIndexedPosition { get; }
		
		IIndexWriter IndexWriter { get; }		
				
		ReadIndexStats GetStatistics();

		IndexReadEventResult ReadEvent(string streamId, long eventNumber);
		IndexReadStreamResult ReadStreamEventsBackward(string streamId, long fromEventNumber, int maxCount);
		IndexReadStreamResult ReadStreamEventsForward(string streamId, long fromEventNumber, int maxCount);

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

		bool IsStreamDeleted(string streamId);
		long GetStreamLastEventNumber(string streamId);
		StreamMetadata GetStreamMetadata(string streamId);
		StorageMessage.EffectiveAcl GetEffectiveAcl(string streamId);
		string GetEventStreamIdByTransactionId(long transactionId);

		void Close();
		void Dispose();
	}
}
