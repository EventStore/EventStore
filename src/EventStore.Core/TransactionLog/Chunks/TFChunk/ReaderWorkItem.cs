// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNext;
using DotNext.Buffers;
using DotNext.IO;
using EventStore.Plugins.Transforms;
using static DotNext.Runtime.Intrinsics;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk;

// This is not thread safe. A pooling mechanism in TFChunk ensures that the ReaderWorkItem
// is used by only one thread at a time.
internal sealed class ReaderWorkItem : Disposable {
	private const int BufferSize = 4096;
	public delegate ValueTask<IChunkHandle> ChunkHandleRefresher(IChunkHandle oldHandle, CancellationToken token);

	// if item was taken from the pool, the field contains position within the array (>= 0)
	private readonly int _positionInPool = -1;
	private readonly IChunkReadTransform _chunkReadTransform;
	private readonly ChunkHandleRefresher _refresher;
	private readonly bool _leaveOpen;

	private IChunkHandle _handle;
	private IBufferedReader _cachedReader;
	private ChunkDataReadStream _baseStream;

	public ChunkDataReadStream BaseStream {
		get => _baseStream;
		set {
			Debug.Assert(value is not null);

			_baseStream = value;

			// Access to the internal buffer of 'PoolingBufferedStream' is only allowed
			// when the top-level stream doesn't perform any transformations. Otherwise,
			// the buffer contains untransformed bytes that cannot be accessed directly.
			_cachedReader = IsExactTypeOf<ChunkDataReadStream>(BaseStream)
							&& BaseStream.ChunkFileStream is PoolingBufferedStream bufferedStream
				? bufferedStream
				: null;
		}
	}

	private ReaderWorkItem(IChunkReadTransform chunkReadTransform, bool leaveOpen) {
		_chunkReadTransform = chunkReadTransform;
		_leaveOpen = leaveOpen;
	}

	public ReaderWorkItem(Stream sharedStream, IChunkReadTransform chunkReadTransform)
		: this(chunkReadTransform, leaveOpen: true) {

		BaseStream = CreateTransformedMemoryStream(sharedStream);
		IsMemory = true;
	}

	public ReaderWorkItem(IChunkHandle handle, ChunkHandleRefresher refresher, IChunkReadTransform chunkReadTransform)
		: this(chunkReadTransform, leaveOpen: false) {

		_handle = handle;
		_refresher = refresher;
		BaseStream = CreateTransformedFileStream();
		IsMemory = false;
	}

	//qq consider name
	//qq call this when the stream has thrown an exception indicating that the handle has gone bad
	// it grabs the latest handle and creates a new stream around it.
	public async ValueTask Refresh(CancellationToken token) {
		if (IsMemory)
			throw new InvalidOperationException("Cannot refresh InMemory ReaderWorkItem");

		if (!_leaveOpen) {
			//qq check this, we do need to dispose the stream i think?
			//qq what disposes the handle? not disposing this stream
			BaseStream.Dispose();
		}

		// the handle we have got has gone bad, we need to get a new one.
		_handle = await _refresher.Invoke(_handle, token);
		BaseStream = CreateTransformedFileStream();
	}

	private ChunkDataReadStream CreateTransformedMemoryStream(Stream memStream) {
		return _chunkReadTransform.TransformData(new ChunkDataReadStream(memStream));
	}

	private ChunkDataReadStream CreateTransformedFileStream() {
		var fileStream = new PoolingBufferedStream(_handle.CreateStream()) { MaxBufferSize = BufferSize };
		return _chunkReadTransform.TransformData(new ChunkDataReadStream(fileStream));
	}

	public bool IsMemory { get; }

	public int PositionInPool {
		get => _positionInPool;
		init {
			Debug.Assert(value >= 0);

			_positionInPool = value;
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

	protected override void Dispose(bool disposing) {
		if (disposing) {
			if (!_leaveOpen)
				BaseStream.Dispose();
		}

		base.Dispose(disposing);
	}
}
