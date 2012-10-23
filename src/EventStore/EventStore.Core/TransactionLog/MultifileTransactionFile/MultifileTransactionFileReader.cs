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
using System.Diagnostics;
using System.IO;
using EventStore.Common.Utils;
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.MultifileTransactionFile
{
    public class MultifileTransactionFileReader : IDisposable, ITransactionFileReader
    {
        private readonly TransactionFileDatabaseConfig _config;

        private readonly int _bufferSize;
        private readonly byte[] _tmpBuffer;
        private readonly MemoryStream _buffer;
        private readonly BinaryReader _bufferReader;

        private FileStream _currentStream;
        private BinaryReader _reader;

        private long _curPos = -1;
        private long _curLocalPos = -1;
        private int _curFileIndex = -1;

        private readonly ICheckpoint _checkpoint;
        private long _lastCheck;

        private readonly long _segmentSize;

        public MultifileTransactionFileReader(TransactionFileDatabaseConfig config, ICheckpoint checkpoint)
            : this(config, checkpoint, 8096) { }

        public MultifileTransactionFileReader(TransactionFileDatabaseConfig config, ICheckpoint checkpoint, int bufferSize)
        {
            Ensure.NotNull(config, "config");
            Ensure.NotNull(checkpoint, "checkpoint");

            _config = config;
            _segmentSize = config.SegmentSize;
            _bufferSize = bufferSize;
            _tmpBuffer = new byte[bufferSize];
            _buffer = new MemoryStream();
            _bufferReader = new BinaryReader(_buffer);

            _checkpoint = checkpoint;
            _lastCheck = _checkpoint.Read();
        }

        public void Open()
        {
            if (!SetPosition(0))
                throw new CorruptDatabaseException(
                    new ChunkNotFoundException(_config.FileNamingStrategy.GetFilenameFor(0)));
        }

        private bool SetPosition(long position)
        {
            if (_curPos >= 0 && _lastCheck - _curPos <= _bufferSize && _currentStream != null)
            {
                //_currentStream.Flush(); // flush read buffers
                // Flush read buffers. On Mono Flush() doesn't do it for <= v2.10.x
                _currentStream.Position = _curLocalPos;
            }

            if (_curPos == position)
                return true;

            _curPos = position;
            _curLocalPos = position % _segmentSize;
            var fileIndex = (int)(position / _segmentSize);
            if (fileIndex != _curFileIndex)
            {
                _curFileIndex = fileIndex;
                return TryOpenFileByIndex(_curFileIndex, _curLocalPos);
            }

            _currentStream.Seek(_curLocalPos, SeekOrigin.Begin);
            return true;
        }

        public RecordReadResult TryReadAt(long position)
        {
            if (!SetPosition(position))
                return new RecordReadResult(false, _curPos, null, 0);

            _lastCheck = _checkpoint.Read();

            if (!TryReadNextBytes(4))
                return new RecordReadResult(false, _curPos, null, 0);

            var length = _bufferReader.ReadInt32();
            if (length < 0)
                throw new ArgumentOutOfRangeException("length", string.Format("Log record has negative length: {0}. Something is seriously wrong.", length));
            if (length > TFConsts.MaxLogRecordSize)
                throw new ArgumentOutOfRangeException("length", string.Format("Log record length is too large: {0} bytes, while limit is {1} bytes.", length, TFConsts.MaxLogRecordSize));
            
            if (!TryReadNextBytes(length + 4))
                return new RecordReadResult(false, _curPos, null, 0);

            var record = LogRecord.ReadFrom(_bufferReader);

            var suffixLength = _bufferReader.ReadInt32();
            Debug.Assert(suffixLength == length);

            return new RecordReadResult(true, _curPos, record, length);
        }

        private bool TryReadNextBytes(int length)
        {
            if (_curPos + length > _lastCheck)
                return false;

            _buffer.Position = 0;

            var leftToRead = length;
            while (leftToRead > 0)
            {
                var leftInFile = _segmentSize - _curLocalPos;
                var toRead = (int)Math.Min(Math.Min(leftInFile, _tmpBuffer.Length), leftToRead);
                var read = _reader.Read(_tmpBuffer, 0, toRead);
                _buffer.Write(_tmpBuffer, 0, read);
                leftToRead -= read;

                if (!TryAdjustPosition(read))
                    return false;
            }

            _buffer.Position = 0;
            return true;
        }

        private bool TryAdjustPosition(int bytesRead)
        {
            int oldFileIndex = _curFileIndex;

            _curPos += bytesRead;
            _curLocalPos += bytesRead;
            while (_curLocalPos >= _segmentSize)
            {
                _curFileIndex++;
                _curLocalPos -= _segmentSize;
            }
            if (_curFileIndex != oldFileIndex)
                return TryOpenFileByIndex(_curFileIndex, _curLocalPos);
            return true;
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

                _curPos = -1;
                _curLocalPos = -1;
                _curFileIndex = -1;
                
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