// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Diagnostics;
using System.IO;
using DotNext;
using DotNext.Buffers;
using DotNext.IO;
using EventStore.Plugins.Transforms;
using static DotNext.Runtime.Intrinsics;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk;

internal sealed class ReaderWorkItem : Disposable {
	private const int BufferSize = 4096;

	// if item was taken from the pool, the field contains position within the array (>= 0)
	private readonly int _positionInPool = -1;
	public readonly ChunkDataReadStream BaseStream;
	public readonly ChunkDataReadStream PosMapStream;
	private readonly bool _leaveOpen;
	private readonly IBufferedReader _cachedReader;

	public ReaderWorkItem(Stream sharedStream, IChunkReadTransform chunkReadTransform) {
		IsMemory = true;
		_leaveOpen = true;
		BaseStream = PosMapStream = chunkReadTransform.TransformData(new ChunkDataReadStream(sharedStream));
	}

	public ReaderWorkItem(IChunkHandle handle, IChunkReadTransform chunkReadTransform, bool scavenged) {
		var source = handle.CreateStream();
		BaseStream = chunkReadTransform.TransformData(new ChunkDataReadStream(new PoolingBufferedStream(source) {
			MaxBufferSize = BufferSize,
		}));

		PosMapStream = scavenged
			? chunkReadTransform.TransformData(
				new ChunkDataReadStream(new PoolingBufferedStream(source, leaveOpen: true) {
					MaxBufferSize = BufferSize
				}))
			: BaseStream;

		// Access to the internal buffer of 'PoolingBufferedStream' is only allowed
		// when the top-level stream doesn't perform any transformations. Otherwise,
		// the buffer contains untransformed bytes that cannot be accessed directly.
		_cachedReader = IsExactTypeOf<ChunkDataReadStream>(BaseStream)
		                && BaseStream.ChunkFileStream is PoolingBufferedStream bufferedStream
			? bufferedStream
			: null;
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
			if (!_leaveOpen) {
				BaseStream.Dispose();
				PosMapStream.Dispose();
			}
		}

		base.Dispose(disposing);
	}
}
