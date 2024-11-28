using System;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Scavenging {
	public class IndexReaderForCalculator<TStreamId> : IIndexReaderForCalculator<TStreamId> {
		private readonly IReadIndex<TStreamId> _readIndex;
		private readonly Func<TFReaderLease> _tfReaderFactory;
		private readonly Func<ulong, TStreamId> _lookupUniqueHashUser;
		private readonly ITransactionFileTracker _tracker;

		public IndexReaderForCalculator(
			IReadIndex<TStreamId> readIndex,
			Func<TFReaderLease> tfReaderFactory,
			Func<ulong, TStreamId> lookupUniqueHashUser,
			ITransactionFileTracker tracker) {

			_readIndex = readIndex;
			_tfReaderFactory = tfReaderFactory;
			_lookupUniqueHashUser = lookupUniqueHashUser;
			_tracker = tracker;
		}

		public long GetLastEventNumber(
			StreamHandle<TStreamId> handle,
			ScavengePoint scavengePoint) {

			switch (handle.Kind) {
				case StreamHandle.Kind.Hash:
					// tries as far as possible to use the index without consulting the log to fetch the last event number
					return _readIndex.GetStreamLastEventNumber_NoCollisions(
						handle.StreamHash,
						_lookupUniqueHashUser,
						scavengePoint.Position, _tracker);
				case StreamHandle.Kind.Id:
					// uses the index and the log to fetch the last event number
					return _readIndex.GetStreamLastEventNumber_KnownCollisions(
						handle.StreamId,
						scavengePoint.Position, _tracker);
				default:
					throw new ArgumentOutOfRangeException(nameof(handle), handle, null);
			}
		}

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
						scavengePoint.Position, _tracker);
				default:
					throw new ArgumentOutOfRangeException(nameof(handle), handle, null);
			}
		}

		public bool IsTombstone(long logPosition) {
			using (var reader = _tfReaderFactory()) {
				var result = reader.TryReadAt(logPosition, couldBeScavenged: true);

				if (!result.Success)
					return false;

				if (result.LogRecord is not IPrepareLogRecord prepare)
					throw new Exception(
						$"Incorrect type of log record {result.LogRecord.RecordType}, " +
						$"expected Prepare record.");

				return prepare.Flags.HasAnyOf(PrepareFlags.StreamDelete);
			}
		}
	}
}
