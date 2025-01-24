// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Buffers;
using DotNext.Diagnostics;
using DotNext.IO;
using DotNext.Runtime.InteropServices;
using EventStore.Common.Utils;
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Plugins.Transforms;
using Serilog;
using static DotNext.Runtime.Intrinsics;

namespace EventStore.Core.TransactionLog.Chunks;

//qq this shouldn't have to exist
class MyChunkDataReadStream : ChunkDataReadStream {
	private readonly Stream _stream;

	public MyChunkDataReadStream(Stream stream) : base(stream) {
		_stream = stream;
	}

	public override long Length => _stream.Length;
}

//qq consider how this will work with encryption at rest
//qqqq this scans through a chunk one record at a time.
// while it is in the middle of a chunk it holds a reader open on that chunk
// so not intended to be taken out of the pool and kept for extended periods of time.
public sealed class MyTFChunkReader : ITransactionFileReader, IDisposable {
	internal static long CachedReads;
	internal static long NotCachedReads;

	public const int MaxRetries = 20;

	private static readonly ILogger Log = Serilog.Log.ForContext<TFChunkReader>();

	private readonly TFChunkDb _db;
	private readonly IReadOnlyCheckpoint _writerCheckpoint;
	private long _curPos; // global log position of the next record we will try to read. not sure we need to maintain this

	//qq whether we use Data or Raw probably depends on what it easiest with encryptionatrest
	private TFChunkBulkDataReader _currentReader;
	private IBufferedReader _cachedReader; //qq invariant: _currentReader not null => _cachedReader valid

	//qq consider whether we want to have a large buffer like this in unmanaged memory
	// or just rent/return smaller buffers as we need
	const int BufferSize = 16 * 1024 * 1024;
	private readonly IUnmanagedMemory<byte> _buffer;

	public MyTFChunkReader(TFChunkDb db, IReadOnlyCheckpoint writerCheckpoint, long initialPosition = 0) {
		Ensure.NotNull(db, "dbConfig");
		Ensure.NotNull(writerCheckpoint, "writerCheckpoint");
		Ensure.Nonnegative(initialPosition, "initialPosition");

		_buffer = UnmanagedMemory.Allocate<byte>(BufferSize);
		GC.AddMemoryPressure(BufferSize);

		_db = db;
		_writerCheckpoint = writerCheckpoint;
		_curPos = initialPosition;
	}

	public void Dispose() {
		_currentReader?.Dispose();
		_buffer.Dispose();
		GC.RemoveMemoryPressure(BufferSize);
	}

	public void Reposition(long position) {
		_curPos = position;
		_currentReader?.Dispose();
		_currentReader = null;
	}

	public ValueTask<SeqReadResult> TryReadNext(CancellationToken token)
		=> TryReadNextInternal(0, token);

	private async ValueTask<SeqReadResult> TryReadNextInternal(int retries, CancellationToken token) {
		// loop allows us to transition to next chunk
		while (true) {
			var writerChk = _writerCheckpoint.Read();
			if (_curPos >= writerChk)
				return SeqReadResult.Failure;


			ILogRecord record;
			int length;

			// invariant: _currentReader not null => _currentReader is for the right chunk and is in the right position.
			if (_currentReader is null) {
				// we don't have a _currentReader or it is for another chunk.
				var start = new Timestamp();
				_currentReader?.Dispose();
				//qq other stuff from TFChunkReader
				// - file being deleted exception
				// - CountRead, tracker, etc.
				var chunk = _db.Manager.GetChunkFor(_curPos);

				//qq we might be able to deal with raw and miss out the streamsegment
				_currentReader = await chunk.AcquireDataReader(token) as TFChunkBulkDataReader;
				// Access to the internal buffer of 'PoolingBufferedStream' is only allowed
				// when the top-level stream doesn't perform any transformations. Otherwise,
				// the buffer contains untransformed bytes that cannot be accessed directly.
				_cachedReader =
					_currentReader.MyStream is StreamSegment streamSegment &&
					streamSegment.BaseStream is ChunkDataReadStream cdrs &&
					IsExactTypeOf<MyChunkDataReadStream>(cdrs) &&
					cdrs.ChunkFileStream is IBufferedReader bufferedReader
						? bufferedReader
						: null;

				//qqqqq this is taking up 1.5 seconds, which is a lot.
				//qqq need to position the stream.
				//qqq rather have a seeking mechanism but we'd need some cooperation from the underlying reader
				// for now just read until we are in position
				// EndOfStreamException here is unexpected and we let it throw out

				var scan = false;
				if (scan) {
					var skipped = 0;
					(record, length) = await ConsumeRecordFromStream(token);
					while (record.LogPosition < _curPos) {
						skipped++;
						(record, length) = await ConsumeRecordFromStream(token);
					}
					Log.Warning("positioning reader for chunk {chunk} skipped {skipped}", chunk.ChunkLocator, skipped);

				} else {
					//qq smallest hack way of doing this was to hijack TryReadClosestForward
					var res = await chunk.TryReadClosestForward(chunk.ChunkHeader.GetLocalLogPosition(_curPos), token);
					//qq maybe use raw and add the header here instead
					_currentReader.MyStream.Position = res.ActualPosition.Value; //qq may be null
					(record, length) = await ConsumeRecordFromStream(token);
					if (res.LogRecord.LogPosition != record.LogPosition)
						throw new Exception("jlkfghjklfgh");
				}

				Log.Warning("positioning reader for chunk {chunk} took {elapsed}",
					chunk.ChunkLocator, start.ElapsedMilliseconds);
				// we've read the next record, we can return it.

			} else {
				// we have a reader in the right place, just read the next record.
				try {
					(record, length) = await ConsumeRecordFromStream(token);
				} catch (EndOfStreamException) {
					//qq best way to detect end of stream?
					// reached the end of the stream.
					Reposition(_currentReader.Chunk.ChunkHeader.ChunkEndPosition);
					continue;
				}
			}

			var postPos = record.GetNextLogPosition(record.LogPosition, length);
			var eof = postPos == writerChk;
			return new SeqReadResult(
				success: true,
				eof: eof,
				logRecord: record,
				recordLength: length,
				recordPrePosition: record.LogPosition,
				recordPostPosition: postPos); //qq are these three as redundant as they seem
		}
	}

	internal IBufferedReader TryGetBufferedReader(int length, out ReadOnlyMemory<byte> buffer) {
		if (_cachedReader is { } reader) {
			buffer = reader.Buffer.TrimLength(length);
		} else {
			buffer = ReadOnlyMemory<byte>.Empty;
			reader = null;
		}

		return buffer.Length >= length ? reader : null;
	}

	//qq similar to TFChunkReadSide.TryReadForwardInternal, we can probably refactor.
	// TFChunkBulkDataReader.ReadNextBytes is more complicated, revist
	private async ValueTask<(ILogRecord, int)> ConsumeRecordFromStream(CancellationToken token) {
		var stream = _currentReader.MyStream;
		var buffer = _buffer.Memory;

		var length = await stream.ReadLittleEndianAsync<int>(buffer, token);
		var lengthWithSuffix = length + sizeof(int);

		IBufferedReader bufferedReader = null;
		try {
			//qq measure how much difference this is making
			if ((bufferedReader = TryGetBufferedReader(lengthWithSuffix, out var input)) is null) {
				var recordWithSuffixBuffer = buffer[..lengthWithSuffix];
				await stream.ReadExactlyAsync(recordWithSuffixBuffer, token);
				input = recordWithSuffixBuffer;
			} else {
				;
			}

			var reader = new SequenceReader(new(input));
			//qq is there a reason this doesn't read straight from the stream, would it save a buffer copy
			var record = LogRecord.ReadFrom(ref reader);
			//qq trackers
			// CountRead

			var lengthSuffix = BinaryPrimitives.ReadInt32LittleEndian(input[^sizeof(int)..].Span);

			if (length != lengthSuffix)
				throw new Exception("fghjjyg");//qq details

			return (record, length);
		} finally {
			bufferedReader?.Consume(lengthWithSuffix);
		}
	}

	public ValueTask<SeqReadResult> TryReadPrev(CancellationToken token) {
		//qq do we ever 
		throw new NotImplementedException();
	}

	public ValueTask<RecordReadResult> TryReadAt(long position, bool couldBeScavenged, CancellationToken token)
		=> TryReadAtInternal(position, couldBeScavenged, 0, token);

	private async ValueTask<RecordReadResult> TryReadAtInternal(long position, bool couldBeScavenged, int retries, CancellationToken token) {
		var writerChk = _writerCheckpoint.Read();
		if (position >= writerChk) {
			Log.Warning(
				"Attempted to read at position {position}, which is further than writer checkpoint {writerChk}",
				position, writerChk);
			return RecordReadResult.Failure;
		}

		var chunk = _db.Manager.GetChunkFor(position);
		try {
			CountRead(chunk.IsCached);
			return await chunk.TryReadAt(chunk.ChunkHeader.GetLocalLogPosition(position), couldBeScavenged, token);
		} catch (FileBeingDeletedException) {
			if (retries > MaxRetries)
				throw new FileBeingDeletedException(
					"Been told the file was deleted > MaxRetries times. Probably a problem in db.");
			return await TryReadAtInternal(position, couldBeScavenged, retries + 1, token);
		}
	}

	public ValueTask<bool> ExistsAt(long position, CancellationToken token)
		=> ExistsAtInternal(position, 0, token);

	private async ValueTask<bool> ExistsAtInternal(long position, int retries, CancellationToken token) {
		var writerChk = _writerCheckpoint.Read();
		if (position >= writerChk)
			return false;

		var chunk = _db.Manager.GetChunkFor(position);
		try {
			CountRead(chunk.IsCached);
			return await chunk.ExistsAt(chunk.ChunkHeader.GetLocalLogPosition(position), token);
		} catch (FileBeingDeletedException) {
			if (retries > MaxRetries)
				throw new FileBeingDeletedException(
					"Been told the file was deleted > MaxRetries times. Probably a problem in db.");
			return await ExistsAtInternal(position, retries + 1, token);
		}
	}

	private static void CountRead(bool isCached) {
		if (isCached)
			Interlocked.Increment(ref CachedReads);
		else
			Interlocked.Increment(ref NotCachedReads);
	}
}
