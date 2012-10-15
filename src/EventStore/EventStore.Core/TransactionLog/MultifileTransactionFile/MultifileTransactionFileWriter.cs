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
using EventStore.Common.Log;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.MultifileTransactionFile
{
    public class MultifileTransactionFileWriter: ITransactionFileWriter
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<MultifileTransactionFileWriter>();

        private readonly TransactionFileDatabaseConfig _config;
        private readonly long _segmentSize;
        private readonly int _bufferSize;
        private FileStream _fileStream;
        private readonly MemoryStream _buffer;
        private readonly BinaryWriter _bufferWriter;
        private int _currentSegment;

        private long _writerCheck;
        private long _writerLocalCheck;
        private readonly ICheckpoint _writerCheckpoint;

        public ICheckpoint Checkpoint { get { return _writerCheckpoint; } }

        public MultifileTransactionFileWriter(TransactionFileDatabaseConfig config) : this(config, 8096) { }

        public MultifileTransactionFileWriter(TransactionFileDatabaseConfig config, Int32 bufferSize)
        {
            if (config == null) 
                throw new ArgumentNullException("config");

            _config = config;
            _segmentSize = config.SegmentSize;
            _writerCheckpoint = config.WriterCheckpoint;

            _bufferSize = bufferSize;
            _buffer = new MemoryStream(1024);
            _bufferWriter = new BinaryWriter(_buffer);
        }

        public void Open()
        {
            //we assume here that validation has been run and that we have a valid database.
            _writerCheck = _writerCheckpoint.Read();
            _writerLocalCheck = _writerCheck % _segmentSize;

            _currentSegment = (int)(_writerCheck / _segmentSize);
            if (_currentSegment == 0 && !File.Exists(GetFileNameFor(0)))
            {
                CreateSegment(0);
            }
            else
            {
                OpenSegment(_currentSegment);
            }
        }

        private void CreateSegment(int segmentIndex)
        {
            var sw = Stopwatch.StartNew();

            CloseFile();
            var fileName = GetFileNameFor(segmentIndex);
            _fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Read, _bufferSize, FileOptions.SequentialScan);
            _fileStream.SetLength(_segmentSize);
            
            sw.Stop();
            Log.Trace("TF Expansion took: {0}.", sw.Elapsed);
        }

        private void OpenSegment(int segmentIndex)
        {
            var fileName = GetFileNameFor(segmentIndex);
            _fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Write, FileShare.Read, _bufferSize, FileOptions.SequentialScan);
            _fileStream.Seek(_writerLocalCheck, SeekOrigin.Begin);
        }

        private string GetFileNameFor(int number)
        {
            return _config.FileNamingStrategy.GetFilenameFor(number);
        }

        public bool Write(LogRecord record, out long newPos)
        {
            _buffer.SetLength(4);
            _buffer.Position = 4;
            record.WriteTo(_bufferWriter);
            var length = (int) _buffer.Length - 4;
            _bufferWriter.Write(length); // length suffix
            _buffer.Position = 0;
            _bufferWriter.Write(length); // length prefix

            var written = 0;
            var leftToWrite = length + 8;

            while (leftToWrite > 0)
            {
                var leftInFile = (int)(_segmentSize - _writerLocalCheck);
                var amountToWrite = leftToWrite > leftInFile ? leftInFile : leftToWrite;
                
                _fileStream.Write(_buffer.GetBuffer(), written, amountToWrite);
                
                leftToWrite -= amountToWrite;
                written += amountToWrite;

                _writerCheck += amountToWrite;
                _writerLocalCheck += amountToWrite;
                if (_writerLocalCheck >= _segmentSize)
                {
                    _writerLocalCheck -= _segmentSize;
                    _currentSegment++;
                    CreateSegment(_currentSegment);
                }
            }
            _writerCheckpoint.Write(_writerCheck);
            newPos = _writerCheck;
            return true;
        }

        public void Close()
        {
            Flush();
            CloseFile();
            _writerCheckpoint.Close();
        }

        private void CloseFile()
        {
            try
            {
                if (_fileStream != null)
                {
                    //no need to sync as checkpoint has not been written
                    _fileStream.Close();
                    _fileStream.Dispose();
                }
            }
            catch (Exception)
            {
                _fileStream = null;
            }
        }

        public void Flush()
        {
            _fileStream.Flush(flushToDisk: true);
#if LESS_THAN_NET_4_0
            Win32.FlushFileBuffers(_fileStream.SafeFileHandle);
#endif
            _writerCheckpoint.Flush();
        }

        public void Dispose()
        {
            Close();
        }
    }
}