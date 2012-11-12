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
using System.Net;
using EventStore.Common.Utils;

namespace EventStore.Core.TransactionLog.Checkpoint
{
    public class FileMultiCheckpoint : IMultiCheckpoint
    {
        private const int IPAddressSize = 4;
        private const int EntrySize = 8 /* Checkpoint */ + IPAddressSize + 4 /* Port */;

        public string Name { get { return _name; } }

        private readonly string _name;
        private readonly string _filename;
        private readonly FileStream _fileStream;

        private readonly BinaryWriter _writer;
        private readonly BinaryReader _reader;

        private readonly InMemMultiCheckpoint _memCheckpoint;

        private bool _dirty = true;

        public FileMultiCheckpoint(string filename, int bestCount)
            : this(filename, Guid.NewGuid().ToString(), bestCount)
        {
        }

        public FileMultiCheckpoint(string filename, string name, int bestCount)
        {
            Ensure.NotNullOrEmpty(filename, "filename");
            Ensure.NotNull(name, "name");
            Ensure.Positive(bestCount, "bestCount");

            _filename = filename;
            _name = name;
            _memCheckpoint = new InMemMultiCheckpoint(string.Format("{0} - InMem", name), bestCount);

            _fileStream = new FileStream(_filename, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

            if (_fileStream.Length % EntrySize != 0)
            {
                throw new Exception(
                        string.Format("MultiCheckpoint file '{0}' has length {1} not divisible by EntrySize ({2}).",
                                      _filename,
                                      _fileStream.Length,
                                      EntrySize));
            }

            _reader = new BinaryReader(_fileStream);
            _writer = new BinaryWriter(_fileStream);

            ReadCheckpoints((int)_fileStream.Length / EntrySize);
        }

        private void ReadCheckpoints(int cnt)
        {
            _fileStream.Seek(0, SeekOrigin.Begin);
            var ipAddrBytes = new byte[IPAddressSize];
            for (int i=0; i<cnt; ++i)
            {
                var checkpoint = _reader.ReadInt64();
                if (checkpoint < 0)
                {
                    throw new Exception(string.Format("Read negative checkpoint with value {0} from file {1} at FileMultiCheckpoint '{2}'.",
                                                      checkpoint,
                                                      _filename,
                                                      _name));
                }

                int read = _reader.Read(ipAddrBytes, 0, IPAddressSize);
                if (read != IPAddressSize)
                {
                    throw new Exception(string.Format("Expected to read {0} bytes for IPAddress, but read {1} bytes "
                                                      + "from file {2} at FileMultiCheckpoint '{3}'.",
                                                      IPAddressSize,
                                                      read,
                                                      _filename,
                                                      _name));
                }

                int port = _reader.ReadInt32();
                _memCheckpoint.Update(new IPEndPoint(new IPAddress(ipAddrBytes), port), checkpoint);
            }
        }

        public void Dispose()
        {
            Close();
        }

        public void Close()
        {
            Flush();
            _reader.Close();
            _writer.Close();
            _fileStream.Close();
        }

        public bool TryGetMinMax(out long checkpoint)
        {
            return _memCheckpoint.TryGetMinMax(out checkpoint);
        }

        public void Update(IPEndPoint endPoint, long checkpoint)
        {
            _dirty = true;
            _memCheckpoint.Update(endPoint, checkpoint);
        }

        public void Clear()
        {
            _dirty = true;
            _memCheckpoint.Clear();
        }

        public void Flush()
        {
            if (!_dirty)
                return;

            _fileStream.SetLength(EntrySize * _memCheckpoint.CheckpointCount);
            _fileStream.Seek(0, SeekOrigin.Begin);

            foreach(var check in _memCheckpoint.CurrentCheckpoints)
            {
                _writer.Write(check.Item1);
                _writer.Write(check.Item2.Address.GetAddressBytes());
                _writer.Write(check.Item2.Port);
            }
            _fileStream.Flush(flushToDisk: true);

            _dirty = false;
        }
    }
}