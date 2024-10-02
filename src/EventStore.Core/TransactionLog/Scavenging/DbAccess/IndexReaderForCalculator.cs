// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Scavenging {
	public class IndexReaderForCalculator<TStreamId> : IIndexReaderForCalculator<TStreamId> {
		private readonly IReadIndex<TStreamId> _readIndex;
		private readonly Func<TFReaderLease> _tfReaderFactory;
		private readonly Func<ulong, TStreamId> _lookupUniqueHashUser;

		public IndexReaderForCalculator(
			IReadIndex<TStreamId> readIndex,
			Func<TFReaderLease> tfReaderFactory,
			Func<ulong, TStreamId> lookupUniqueHashUser) {

			_readIndex = readIndex;
			_tfReaderFactory = tfReaderFactory;
			_lookupUniqueHashUser = lookupUniqueHashUser;
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
						scavengePoint.Position);
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
