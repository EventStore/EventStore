// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Diagnostics;
using System.IO;
using System.Text;
using DotNext.IO;
using EventStore.Plugins.Transforms;
using Microsoft.Win32.SafeHandles;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk {
	internal sealed class ReaderWorkItem : BinaryReader {
		public const int BufferSize = 8192;

		// if item was taken from the pool, the field contains position within the array (>= 0)
		private readonly int _positionInPool = -1;

		public unsafe ReaderWorkItem(Stream sharedStream, IChunkReadTransform chunkReadTransform)
			: base(CreateTransformedMemoryStream(sharedStream, chunkReadTransform), Encoding.UTF8, leaveOpen: true) {
			IsMemory = true;
		}

		public ReaderWorkItem(SafeFileHandle handle, IChunkReadTransform chunkReadTransform)
			: base(CreateTransformedFileStream(handle, chunkReadTransform), Encoding.UTF8, leaveOpen: false) {
			IsMemory = false;
		}

		private static Stream CreateTransformedMemoryStream(Stream memStream, IChunkReadTransform chunkReadTransform) {
			return chunkReadTransform.TransformData(new ChunkDataReadStream(memStream));
		}

		private static ChunkDataReadStream CreateTransformedFileStream(SafeFileHandle handle, IChunkReadTransform chunkReadTransform) {
			var fileStream = new BufferedStream(handle.AsUnbufferedStream(FileAccess.Read), BufferSize);
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
	}
}
