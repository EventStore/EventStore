using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.TransactionLog.Scavenging {
	public class IndexReaderForCalculator : IIndexReaderForCalculator<string> {
		private readonly IReadIndex _readIndex;
		private readonly Func<ulong, string> _getStreamId;

		public IndexReaderForCalculator(
			IReadIndex readIndex,
			Func<ulong, IEnumerable<string>> lookupStreamIds) {

			_readIndex = readIndex;
			_getStreamId = hash => {
				//qq review: maybe lookupStreamIds can just return the single element
				var streamIds = lookupStreamIds(hash).ToArray();
				if (streamIds.Length == 0)
					throw new Exception($"Failed to look up stream id for stream hash: {hash}");

				if (streamIds.Length > 1)
					throw new Exception($"Stream hash: {hash} has collisions: {string.Join(",", streamIds)}");

				return streamIds[0];
			};
		}

		public long GetLastEventNumber(
			StreamHandle<string> handle,
			ScavengePoint scavengePoint) {

			switch (handle.Kind) {
				case StreamHandle.Kind.Hash:
					// tries as far as possible to use the index without consulting the log to fetch the last event number
					return _readIndex.GetStreamLastEventNumber_NoCollisions(
						handle.StreamHash,
						_getStreamId,
						scavengePoint.Position);
				case StreamHandle.Kind.Id:
					// uses the index and the log to fetch the last event number
					return _readIndex.GetStreamLastEventNumber_KnownCollisions(
						handle.StreamId,
						scavengePoint.Position);
				default:
					throw new ArgumentOutOfRangeException(nameof(handle), handle, null);
			}
		}

		public IndexReadEventInfoResult ReadEventInfoForward(
			StreamHandle<string> handle,
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
						scavengePoint.Position);
				default:
					throw new ArgumentOutOfRangeException(nameof(handle), handle, null);
			}
		}
	}
}
