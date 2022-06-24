using System;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.TransactionLog.Scavenging {
	public class IndexReaderForAccumulator : IIndexReaderForAccumulator<string> {
		private readonly IReadIndex _readIndex;

		public IndexReaderForAccumulator(IReadIndex readIndex) {
			_readIndex = readIndex;
		}

		public IndexReadEventInfoResult ReadEventInfoForward(
			StreamHandle<string> handle,
			long fromEventNumber,
			int maxCount,
			ScavengePoint scavengePoint) {
			switch (handle.Kind) {
				case StreamHandle.Kind.Hash:
					// uses the index only
					return _readIndex.ReadEventInfoForward_NoCollisions(handle.StreamHash, fromEventNumber, maxCount,
						scavengePoint.Position);
				case StreamHandle.Kind.Id:
					// uses log to check for hash collisions
					return _readIndex.ReadEventInfoForward_KnownCollisions(handle.StreamId, fromEventNumber, maxCount,
						scavengePoint.Position);
				default:
					throw new ArgumentOutOfRangeException(nameof(handle), handle, null);
			}
		}

		public IndexReadEventInfoResult ReadEventInfoBackward(
			string streamId,
			StreamHandle<string> handle,
			long fromEventNumber,
			int maxCount,
			ScavengePoint scavengePoint) {
			switch (handle.Kind) {
				case StreamHandle.Kind.Hash:
					// uses the index only
					return _readIndex.ReadEventInfoBackward_NoCollisions(handle.StreamHash,_ => streamId,
						fromEventNumber, maxCount, scavengePoint.Position);
				case StreamHandle.Kind.Id:
					// uses log to check for hash collisions
					return _readIndex.ReadEventInfoBackward_KnownCollisions(handle.StreamId, fromEventNumber, maxCount,
						scavengePoint.Position);
				default:
					throw new ArgumentOutOfRangeException(nameof(handle), handle, null);
			}
		}
	}
}
