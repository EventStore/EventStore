using System;
using System.IO;
using EventStore.Common.Utils;

namespace EventStore.Core.TransactionLog.Chunks
{
    public class TFChunkBulkReader : IDisposable
    {
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

        public void Release()
        {
            _stream.Close();
            _stream.Dispose();
            _disposed = true;
            _chunk.ReleaseReader(this);
        }

        public BulkReadResult ReadNextPhysicalBytes(int count, byte[] buffer)
        {
            return _chunk.TryReadNextBulkPhysical(_stream, buffer, count);
        }

        public BulkReadResult ReadNextLogicalBytes(int count, byte [] buffer)
        {
            return _chunk.TryReadNextBulkLogical(_stream, buffer, count);
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