using System;
using System.IO;
using EventStore.Common.Utils;

namespace EventStore.Core.TransactionLog.Chunks
{
    public class TFChunkBulkReader : IDisposable
    {
        public TFChunk Chunk { get { return _chunk; } }

        private readonly TFChunk _chunk;
        private readonly Stream _stream;
        private bool _disposed;

        internal TFChunkBulkReader(TFChunk chunk, Stream streamToUse)
        {
            Ensure.NotNull(chunk, "chunk");
            Ensure.NotNull(streamToUse, "stream");
            _chunk = chunk;
            _stream = streamToUse;
        }

        ~TFChunkBulkReader()
        {
            Dispose();
        }

        public void SetLogicalPosition(int logicalPosition)
        {
            var realPos = logicalPosition + ChunkHeader.Size;
            if (realPos > _stream.Length)
                throw new ArgumentOutOfRangeException("logicalPosition", string.Format("Logical position {0} is out of bounds.", logicalPosition));
            _stream.Position = realPos;
        }

        public void Release()
        {
            _stream.Close();
            _stream.Dispose();
            _disposed = true;
            _chunk.ReleaseReader(this);
        }

        public BulkReadResult ReadNextPhysicalBytes(int count, byte[] buffer)
        {
            Ensure.NotNull(buffer, "buffer");
            Ensure.Nonnegative(count, "count");

            if (count > buffer.Length)
                count = buffer.Length;

            var oldPos = (int)_stream.Position;
            int bytesRead = _stream.Read(buffer, 0, count);
            return new BulkReadResult(oldPos, bytesRead, isEof: _stream.Length == _stream.Position);
        }

        public BulkReadResult ReadNextLogicalBytes(int count, byte[] buffer)
        {
            Ensure.NotNull(buffer, "buffer");
            Ensure.Nonnegative(count, "count");

            if (_stream.Position == 0)
                _stream.Position = ChunkHeader.Size;

            if (count > buffer.Length)
                count = buffer.Length;

            var oldPos = (int)_stream.Position - ChunkHeader.Size;
            var mapSize = _chunk.IsReadOnly ? _chunk.ChunkFooter.MapSize : 0;
            var available = (int)(_stream.Length - ChunkFooter.Size - ChunkHeader.Size - mapSize);
            var toRead = available > count ? count : available;
            var bytesRead = _stream.Read(buffer, 0, toRead);
            return new BulkReadResult(oldPos, bytesRead, isEof: available == bytesRead);
        }

        public void Dispose()
        {
            if(_disposed) 
                return;
            Release();
            GC.SuppressFinalize(this);
        }

    }
}