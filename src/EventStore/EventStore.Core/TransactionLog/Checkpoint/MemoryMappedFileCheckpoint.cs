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
using System.IO.MemoryMappedFiles;
using System.Threading;
using EventStore.Common.Utils;

namespace EventStore.Core.TransactionLog.Checkpoint
{
    public class MemoryMappedFileCheckpoint : ICheckpoint
    {
        public string Name { get { return _name; } }

        private readonly string _filename;
        private readonly string _name;
        private readonly bool _cached;
        private readonly FileStream _fileStream;
        private readonly MemoryMappedFile _file;
        private long _last;
        private long _lastFlushed;
        private readonly MemoryMappedViewAccessor _accessor;

        private readonly object _flushLocker = new object();

        public MemoryMappedFileCheckpoint(string filename): this(filename, Guid.NewGuid().ToString(), false)
        {
        }

        public MemoryMappedFileCheckpoint(string filename, string name, bool cached, bool mustExist = false, long initValue = 0)
        {
            _filename = filename;
            _name = name;
            _cached = cached;
            var old = File.Exists(_filename);
            _fileStream = new FileStream(_filename,
                                         mustExist ? FileMode.Open : FileMode.OpenOrCreate,
                                         FileAccess.ReadWrite,
                                         FileShare.ReadWrite);
            _fileStream.SetLength(sizeof(long));
            _file = MemoryMappedFile.CreateFromFile(_fileStream,
                                                    Guid.NewGuid().ToString(),
                                                    sizeof(long),
                                                    MemoryMappedFileAccess.ReadWrite,
                                                    new MemoryMappedFileSecurity(),
                                                    HandleInheritability.None,
                                                    false);
            _accessor = _file.CreateViewAccessor(0, sizeof(long));

            if (old)
                _last = _lastFlushed = ReadCurrent();
            else
            {
                _last = initValue;
                Flush();
            }
        }

        public void Close()
        {
            Flush();
            _accessor.Dispose();
            _file.Dispose();
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

            _accessor.Write(0, last);
            _accessor.Flush();

            _fileStream.FlushToDisk();
//            if (!FileStreamExtensions.FlushFileBuffers(_fileHandle))
//                throw new Exception(string.Format("FlushFileBuffers failed with err: {0}", Marshal.GetLastWin32Error()));

            Interlocked.Exchange(ref _lastFlushed, last);

            lock (_flushLocker)
            {
                Monitor.PulseAll(_flushLocker);
            }
        }

        public long Read()
        {
            return _cached ? Interlocked.Read(ref _lastFlushed) : ReadCurrent();
        }

        private long ReadCurrent()
        {
            return _accessor.ReadInt64(0);
        }

        public long ReadNonFlushed()
        {
            return Interlocked.Read(ref _last);
        }

        public bool WaitForFlush(TimeSpan timeout)
        {
            lock (_flushLocker)
            {
                return Monitor.Wait(_flushLocker, timeout);
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}