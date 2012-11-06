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
using System.Threading;

namespace EventStore.Core.TransactionLog.Checkpoint
{
    public class FileCheckpoint : ICheckpoint
    {
        private readonly string _name;
        private readonly string _filename;
        private readonly FileStream _fileStream;
        
        private long _last;
        private long _lastFlushed;
        private readonly bool _cached;
        
        private readonly BinaryWriter _writer;
        private readonly BinaryReader _reader;

        public FileCheckpoint(string filename)
            : this(filename, Guid.NewGuid().ToString())
        {
        }

        public FileCheckpoint(string filename, string name, bool cached = false)
        {
            _filename = filename;
            _name = name;
            _cached = cached;
            var old = File.Exists(filename);
            _fileStream = new FileStream(_filename, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            if (_fileStream.Length != 8)
                _fileStream.SetLength(8);
            _reader = new BinaryReader(_fileStream);
            _writer = new BinaryWriter(_fileStream);
            if (old)
                _lastFlushed = _last = ReadCurrent();
        }

        private long ReadCurrent()
        {
            _fileStream.Seek(0, SeekOrigin.Begin);
            return _reader.ReadInt64();
        }

        public void Close()
        {
            Flush();
            _reader.Close();
            _writer.Close();
            _fileStream.Close();
        }

        public string Name
        {
            get { return _name; }
        }

        public void Write(long checkpoint)
        {
            Interlocked.Exchange(ref _last, checkpoint);
        }

        public void Flush()
        {
            var last = Interlocked.Read(ref _last);
            if (last == _lastFlushed)
                return;

            _fileStream.Seek(0, SeekOrigin.Begin);
            _writer.Write(last);

            _fileStream.Flush(flushToDisk: true);
#if LESS_THAN_NET_4_0
            Win32.FlushFileBuffers(_fileStream.SafeFileHandle);
#endif
            Interlocked.Exchange(ref _lastFlushed, last);
        }

        public long Read()
        {
            return _cached ? Interlocked.Read(ref _lastFlushed) : ReadCurrent();
        }

        public long ReadNonFlushed()
        {
            return Interlocked.Read(ref _last);
        }

        public void Dispose()
        {
            Close();
        }
    }
}