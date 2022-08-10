﻿using System.Collections.Generic;
using EventStore.Core.TransactionLog.Scavenging;

namespace EventStore.Core.XUnit.Tests.Scavenge {
	public class TrackingChunkReaderForExecutor<TStreamId, TRecord> :
		IChunkReaderForExecutor<TStreamId, TRecord> {

		private readonly IChunkReaderForExecutor<TStreamId, TRecord> _wrapped;
		private readonly Tracer _tracer;

		public TrackingChunkReaderForExecutor(
			IChunkReaderForExecutor<TStreamId, TRecord> wrapped,
			Tracer tracer) {

			_wrapped = wrapped;
			_tracer = tracer;
		}

		public string Name => _wrapped.Name;

		public int FileSize => _wrapped.FileSize;

		public int ChunkStartNumber => _wrapped.ChunkStartNumber;

		public int ChunkEndNumber => _wrapped.ChunkEndNumber;

		public bool IsReadOnly => _wrapped.IsReadOnly;

		public long ChunkStartPosition => _wrapped.ChunkStartPosition;

		public long ChunkEndPosition => _wrapped.ChunkEndPosition;

		public IEnumerable<bool> ReadInto(
			RecordForExecutor<TStreamId, TRecord>.NonPrepare nonPrepare,
			RecordForExecutor<TStreamId, TRecord>.Prepare prepare) {

			_tracer.Trace($"Opening Chunk {ChunkStartNumber}-{ChunkEndNumber}");
			return _wrapped.ReadInto(nonPrepare, prepare);
		}
	}
}
