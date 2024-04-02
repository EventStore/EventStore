using System;
using System.IO;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Chunks.TFChunk;

namespace EventStore.Core.TransactionLog.Chunks {
	public abstract class TFChunkBulkReader : IDisposable {
		public TFChunk.TFChunk Chunk {
			get { return _chunk; }
		}

		internal Stream Stream {
			get { return _stream; }
		}

		private readonly TFChunk.TFChunk _chunk;
		private readonly Stream _stream;
		private bool _disposed;
		public bool IsMemory { get; init; }

		internal TFChunkBulkReader(TFChunk.TFChunk chunk, Stream streamToUse, bool isMemory) {
			Ensure.NotNull(chunk, "chunk");
			Ensure.NotNull(streamToUse, "stream");
			_chunk = chunk;
			_stream = streamToUse;
			IsMemory = isMemory;
		}

		public abstract void SetPosition(long position);
		public abstract BulkReadResult ReadNextBytes(int count, byte[] buffer);

		~TFChunkBulkReader() {
			Dispose();
		}

		public void Release() {
			_stream.Close();
			_stream.Dispose();
			_disposed = true;
			_chunk.ReleaseReader(this);
		}

		public void Dispose() {
			if (_disposed)
				return;
			Release();
			GC.SuppressFinalize(this);
		}
	}
}
