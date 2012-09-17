// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
using System;
using System.IO;
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog.Checkpoint;

namespace EventStore.Core.TransactionLog.MultifileTransactionFile
{
    public class MultifileTransactionFileBulkRetriever : IDisposable
    {
        private static readonly byte[] Empty = new byte[0];

        private readonly TransactionFileDatabaseConfig _config;
        private readonly int _bulkSize;

        private readonly int _fileBufferSize;
        private readonly byte[] _bulkBuffer;

        private int _curFileIndex = -1;
        private long _curCheck = -1;
        private long _curLocalCheck = -1;

        private FileStream _currentStream;
        private readonly ICheckpoint _writerCheckpoint;
        private long _lastWriterCheck;

        private readonly long _segmentSize;

        public MultifileTransactionFileBulkRetriever(TransactionFileDatabaseConfig config): this(config, 1024, 1024) { }

        public MultifileTransactionFileBulkRetriever(TransactionFileDatabaseConfig config, int bulkSize, int fileBufferSize)
        {
            if (config == null)
                throw new ArgumentNullException("config");
            if (fileBufferSize <= 0)
                throw new ArgumentOutOfRangeException("fileBufferSize");
            if (bulkSize > fileBufferSize)
                throw new ArgumentOutOfRangeException("bulkSize");

            _config = config;
            _bulkSize = bulkSize;
            _bulkBuffer = new byte[_bulkSize];
            _fileBufferSize = fileBufferSize;

            _segmentSize = config.SegmentSize;
            _writerCheckpoint = config.WriterCheckpoint;
            _lastWriterCheck = _writerCheckpoint.Read();
        }

        public void Open(long pos)
        {
            SetPosition(pos);
        }

        public byte[] ReadNextBulk()
        {
            if (_curCheck < 0)
                throw new InvalidOperationException("BulkRetriever was not either opened or some error occured.");
            return ReadBulk(_curCheck);
        }

        public byte[] ReadBulk(long fromPos)
        {
            if (fromPos < 0)
                throw new ArgumentOutOfRangeException("fromPos");

            _lastWriterCheck = _writerCheckpoint.Read();

            var leftToRead = (int) Math.Min(_bulkSize, Math.Max(0, _lastWriterCheck - fromPos));
            if (leftToRead == 0)
                return Empty;

            SetPosition(fromPos);

            //_currentStream.Flush(); // flush read buffers
            // Flush read buffers. On Mono Flush() doesn't do it for <= v2.10.x
            _currentStream.Position = _curLocalCheck;

            var written = 0;
            while (leftToRead > 0)
            {
                var bytesRead = _currentStream.Read(_bulkBuffer, written, leftToRead);
                if (bytesRead == 0)
                    break;

                written += bytesRead;
                leftToRead -= bytesRead;

                AdjustPosition(bytesRead);
            }

            var res = new byte[written];
            Buffer.BlockCopy(_bulkBuffer, 0, res, 0, written);
            return res;
        }

        private void SetPosition(long pos)
        {
            if (_curCheck == pos)
                return;

            var lastFileIndex = _curFileIndex;
            _curCheck = pos;
            _curLocalCheck = pos % _segmentSize;
            _curFileIndex = (int) (pos / _segmentSize);

            if (_curFileIndex != lastFileIndex)
            {
                if (!TryOpenFileByIndex(_curFileIndex, _curLocalCheck))
                {
                    ResetPosition();
                    throw new CorruptDatabaseException(
                        new ChunkNotFoundException(_config.FileNamingStrategy.GetFilenameFor(_curFileIndex)));
                }
            } 
            else
            {
                _currentStream.Seek(_curLocalCheck, SeekOrigin.Begin);
            }
        }

        private void AdjustPosition(int bytesRead)
        {
            _curCheck += bytesRead;
            _curLocalCheck += bytesRead;
            var openNewFile = false;

            while (_curLocalCheck >= _segmentSize)
            {
                _curFileIndex++;
                _curLocalCheck -= _segmentSize;
                openNewFile = true;
            }

            if (openNewFile && !TryOpenFileByIndex(_curFileIndex, _curLocalCheck))
            {
                ResetPosition();
                throw new CorruptDatabaseException(
                    new ChunkNotFoundException(_config.FileNamingStrategy.GetFilenameFor(_curFileIndex)));
            }
        }

        private bool TryOpenFileByIndex(int idx, long pos)
        {
            CloseCurrentStream();

            var filename = _config.FileNamingStrategy.GetFilenameFor(idx);
            try
            {
                _currentStream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, _fileBufferSize, FileOptions.SequentialScan);
                _currentStream.Seek(pos, SeekOrigin.Begin);
            }
            catch (Exception)
            {
                CloseCurrentStream();
                return false;
            }
            return true;
        }

        private void ResetPosition()
        {
            _curCheck = -1;
            _curLocalCheck = -1;
            _curFileIndex = -1;

            CloseCurrentStream();
        }

        public void Dispose()
        {
            Close();
        }

        public void Close()
        {
            CloseCurrentStream();
        }

        private void CloseCurrentStream()
        {
            if (_currentStream != null)
            {
                try
                {
                    _currentStream.Close();
                    _currentStream.Dispose();
                }
                finally
                {
                    _currentStream = null;
                }
            }
        }
    }
}
