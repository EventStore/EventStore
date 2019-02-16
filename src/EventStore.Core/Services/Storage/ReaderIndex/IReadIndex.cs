using System.Security.Principal;
using EventStore.Core.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public interface IReadIndex {
		long LastCommitPosition { get; }
		long LastReplicatedPosition { get; }
		IIndexWriter IndexWriter { get; }

		void Init(long buildToPosition);
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

		bool IsStreamDeleted(string streamId);
		long GetStreamLastEventNumber(string streamId);
		StreamMetadata GetStreamMetadata(string streamId);

		string GetEventStreamIdByTransactionId(long transactionId);
		StreamAccess CheckStreamAccess(string streamId, StreamAccessType streamAccessType, IPrincipal user);

		void Close();
		void Dispose();
	}
}
