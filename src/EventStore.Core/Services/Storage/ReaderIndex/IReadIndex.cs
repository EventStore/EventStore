using System;
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
		IndexReadEventInfoResult ReadEventInfoForward_KnownCollisions(string streamId, long fromEventNumber, int maxCount, long beforePosition);
		IndexReadEventInfoResult ReadEventInfoForward_NoCollisions(ulong stream, long fromEventNumber, int maxCount, long beforePosition);
		IndexReadEventInfoResult ReadEventInfoBackward_KnownCollisions(string streamId, long fromEventNumber, int maxCount, long beforePosition);
		IndexReadEventInfoResult ReadEventInfoBackward_NoCollisions(ulong stream, Func<ulong, string> getStreamId, long fromEventNumber, int maxCount, long beforePosition);

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
		long GetStreamLastEventNumber_KnownCollisions(string streamId, long beforePosition);
		long GetStreamLastEventNumber_NoCollisions(ulong stream, Func<ulong, string> getStreamId, long beforePosition);
		StreamMetadata GetStreamMetadata(string streamId);

		string GetEventStreamIdByTransactionId(long transactionId);
		StreamAccess CheckStreamAccess(string streamId, StreamAccessType streamAccessType, IPrincipal user);

		void Close();
		void Dispose();
	}
}
