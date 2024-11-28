using System;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.TransactionLog.Scavenging {
	public class IndexReaderForAccumulator<TStreamId> : IIndexReaderForAccumulator<TStreamId> {
		private readonly IReadIndex<TStreamId> _readIndex;
		private readonly ITransactionFileTracker _tracker;

		public IndexReaderForAccumulator(IReadIndex<TStreamId> readIndex, ITransactionFileTracker tracker) {
			_readIndex = readIndex;
			_tracker = tracker;
		}

		// reads a stream forward but only returns event info not the full event.
		public IndexReadEventInfoResult ReadEventInfoForward(
			StreamHandle<TStreamId> handle,
			long fromEventNumber,
			int maxCount,
			ScavengePoint scavengePoint) {

			switch (handle.Kind) {
				case StreamHandle.Kind.Hash:
					// uses the index only
					return _readIndex.ReadEventInfoForward_NoCollisions(
						handle.StreamHash,
						fromEventNumber,
						maxCount,
						scavengePoint.Position);
				case StreamHandle.Kind.Id:
					// uses log to check for hash collisions
					return _readIndex.ReadEventInfoForward_KnownCollisions(
						handle.StreamId,
						fromEventNumber,
						maxCount,
						scavengePoint.Position,
						_tracker);
				default:
					throw new ArgumentOutOfRangeException(nameof(handle), handle, null);
			}
		}

		// reads a stream backward but only returns event info not the full event.
		public IndexReadEventInfoResult ReadEventInfoBackward(
			TStreamId streamId,
			StreamHandle<TStreamId> handle,
			long fromEventNumber,
			int maxCount,
			ScavengePoint scavengePoint) {

			switch (handle.Kind) {
				case StreamHandle.Kind.Hash:
					// uses the index only
					return _readIndex.ReadEventInfoBackward_NoCollisions(
						handle.StreamHash,
						_ => streamId,
						fromEventNumber,
						maxCount,
						scavengePoint.Position,
						_tracker);
				case StreamHandle.Kind.Id:
					// uses log to check for hash collisions
					return _readIndex.ReadEventInfoBackward_KnownCollisions(
						handle.StreamId,
						fromEventNumber,
						maxCount,
						scavengePoint.Position,
						_tracker);
				default:
					throw new ArgumentOutOfRangeException(nameof(handle), handle, null);
			}
		}
	}
}
