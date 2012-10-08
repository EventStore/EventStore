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
using EventStore.Common.Utils;
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.MultifileTransactionFile
{
    public class MultifileTransactionFileChaser : IDisposable, ITransactionFileChaser
    {
        private readonly TransactionFileDatabaseConfig _config;

        private readonly ICheckpoint _chaserCheckpoint;

        private readonly int _bufferSize;
        private readonly byte[] _tmpBuffer;
        private readonly MemoryStream _buffer;
        private readonly BinaryReader _bufferReader;

        private FileStream _currentStream;
        private BinaryReader _reader;

        private long _lastChaserCheck;
        private long _lastChaserLocalCheck;
        private int _lastFileIndex;

        private long _curPos;
        private long _curLocalPos;
        private int _curFileIndex;

        private long _lastWriterCheck;

        private readonly long _segmentSize;

        public MultifileTransactionFileChaser(TransactionFileDatabaseConfig config, ICheckpoint chaserCheckpoint)
            : this(config, chaserCheckpoint, 8096)
        {
        }

        public MultifileTransactionFileChaser(TransactionFileDatabaseConfig config, 
                                              ICheckpoint chaserCheckpoint, 
                                              int bufferSize)
        {
            Ensure.NotNull(config, "config");
            Ensure.NotNull(chaserCheckpoint, "chaserCheckpoint");

            _config = config;
            _segmentSize = config.SegmentSize;
            _chaserCheckpoint = chaserCheckpoint;
            _bufferSize = bufferSize;
            _tmpBuffer = new byte[bufferSize];
            _buffer = new MemoryStream();
            _bufferReader = new BinaryReader(_buffer);
        }

        public void Open()
        {
            _lastChaserCheck = _chaserCheckpoint.Read();
            _lastChaserLocalCheck = _lastChaserCheck % _segmentSize;
            _lastFileIndex = (int) (_lastChaserCheck / _segmentSize);

            _curPos = _lastChaserCheck;
            _curLocalPos = _lastChaserLocalCheck;
            _curFileIndex = _lastFileIndex;

            if (!TryOpenFileByIndex(_lastFileIndex, _curLocalPos))
                throw new CorruptDatabaseException(
                    new ChunkNotFoundException(_config.FileNamingStrategy.GetFilenameFor(_lastFileIndex)));
        }

        public bool TryReadNext(out LogRecord record)
        {
            var res = TryReadNext();
            record = res.LogRecord;
            return res.Success;
        }

        public RecordReadResult TryReadNext()
        {
            if (_lastWriterCheck - _curPos <= _bufferSize)
            {
                //_currentStream.Flush(); // flush read buffers
                // Flush read buffers. On Mono Flush() doesn't do it for <= v2.10.x
                _currentStream.Position = _curLocalPos;
            }

            _lastWriterCheck = _config.WriterCheckpoint.Read();

            if (!TryReadNextBytes(4))
                return new RecordReadResult(false, null, _curPos);

            var length = _bufferReader.ReadInt32();
            if (length <= 0 || length > TFConsts.MaxLogRecordSize)
            {
                RestorePosition();
                throw new ArgumentOutOfRangeException("length", "Log record length is out of bounds.");
            }
            
            if (!TryReadNextBytes(length))
                return new RecordReadResult(false, null, _curPos);

            var record = LogRecord.ReadFrom(_bufferReader);
            var logPosition = _lastChaserCheck;

            _lastChaserCheck = _curPos;
            _lastChaserLocalCheck = _curLocalPos;
            _lastFileIndex = _curFileIndex;
            _chaserCheckpoint.Write(_lastChaserCheck);

            return new RecordReadResult(true, record, logPosition);
        }

        private bool TryReadNextBytes(int length)
        {
            if (_curPos + length > _lastWriterCheck)
                return false;

            _buffer.Position = 0;

            var leftToRead = length;
            while (leftToRead > 0)
            {
                var leftInFile = _segmentSize - _curLocalPos;
                if (leftInFile < 0)
                {
                    RestorePosition();
                    throw new InvalidOperationException();
                }

                var toRead = (int)Math.Min(Math.Min(leftInFile, _tmpBuffer.Length), leftToRead);
                var read = _reader.Read(_tmpBuffer, 0, toRead);
                _buffer.Write(_tmpBuffer, 0, read);
                leftToRead -= read;

                if (!TryAdjustPosition(read))
                {
                    RestorePosition();
                    return false;
                }
            }

            _buffer.Position = 0;
            return true;
        }

        private bool TryAdjustPosition(int bytesRead)
        {
            _curPos += bytesRead;
            _curLocalPos += bytesRead;
            while (_curLocalPos >= _segmentSize)
            {
                _curFileIndex++;
                _curLocalPos -= _segmentSize;
            }
            if (_curFileIndex != _lastFileIndex)
                return TryOpenFileByIndex(_curFileIndex, _curLocalPos);
            return true;
        }

        private void RestorePosition()
        {
            if (_curFileIndex != _lastFileIndex)
            {
                if (!TryOpenFileByIndex(_lastFileIndex, _lastChaserLocalCheck))
                    throw new CorruptDatabaseException(
                        new ChunkNotFoundException(_config.FileNamingStrategy.GetFilenameFor(_curFileIndex)));
            }
            else
                _currentStream.Position = _lastChaserLocalCheck; // VERY IMPORTANT! Flushes reader buffer. 

            _curPos = _lastChaserCheck;
            _curLocalPos = _lastChaserLocalCheck;
            _curFileIndex = _lastFileIndex;
        }

        private bool TryOpenFileByIndex(int idx, long pos)
        {
            CloseCurrentStream();

            var filename = _config.FileNamingStrategy.GetFilenameFor(idx);
            try
            {
                _currentStream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, _bufferSize, FileOptions.SequentialScan);
                _currentStream.Seek(pos, SeekOrigin.Begin);
            }
            catch (Exception)
            {
                CloseCurrentStream();
                return false;
            }
            _reader = new BinaryReader(_currentStream);
            return true;
        }

        public void Dispose()
        {
            Close();
        }

        public void Close()
        {
            Flush();
            CloseCurrentStream();
            _chaserCheckpoint.Close();
        }

        public void Flush()
        {
            _chaserCheckpoint.Flush();
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