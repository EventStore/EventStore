// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Diagnostics;
using System.IO;
using DotNext;
using DotNext.IO;
using EventStore.Plugins.Transforms;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk;

internal sealed class ReaderWorkItem : Disposable {
	private const int BufferSize = 512;

	// if item was taken from the pool, the field contains position within the array (>= 0)
	private readonly int _positionInPool = -1;
	public readonly Stream BaseStream;
	private readonly bool _leaveOpen;

	private ReaderWorkItem(Stream stream, bool leaveOpen) {
		Debug.Assert(stream is not null);

		_leaveOpen = leaveOpen;
		BaseStream = stream;
	}

	public ReaderWorkItem(Stream sharedStream, IChunkReadTransform chunkReadTransform)
		: this(CreateTransformedMemoryStream(sharedStream, chunkReadTransform), leaveOpen: true) {
		IsMemory = true;
	}

	public ReaderWorkItem(IChunkHandle handle, IChunkReadTransform chunkReadTransform)
		: this(CreateTransformedFileStream(handle, chunkReadTransform), leaveOpen: false) {
		IsMemory = false;
	}

	private static Stream CreateTransformedMemoryStream(Stream memStream, IChunkReadTransform chunkReadTransform) {
		return chunkReadTransform.TransformData(new ChunkDataReadStream(memStream));
	}

	private static ChunkDataReadStream CreateTransformedFileStream(IChunkHandle handle,
		IChunkReadTransform chunkReadTransform) {
		var fileStream = new PoolingBufferedStream(handle.CreateStream()) { MaxBufferSize = BufferSize };
		return chunkReadTransform.TransformData(new ChunkDataReadStream(fileStream));
	}

	public bool IsMemory { get; }

	public int PositionInPool {
		get => _positionInPool;
		init {
			Debug.Assert(value >= 0);

			_positionInPool = value;
		}
	}

	protected override void Dispose(bool disposing) {
		if (disposing) {
			if (!_leaveOpen)
				BaseStream.Dispose();
		}

		base.Dispose(disposing);
	}
}
