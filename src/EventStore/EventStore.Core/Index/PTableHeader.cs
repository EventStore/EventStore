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
using System.IO;
using EventStore.Core.Exceptions;

namespace EventStore.Core.Index
{
    public class PTableHeader
    {
        public const int Size = 128;

        public readonly FileType FileType;
        public readonly byte Version;

        public PTableHeader(byte version)
        {
            FileType = FileType.PTableFile;
            Version = version;
        }

        public byte[] AsByteArray()
        {
            var array = new byte[Size];
            array[0] = (byte) FileType.PTableFile;
            array[1] = Version;
            return array;
        }

        public static PTableHeader FromStream(Stream stream)
        {
            var type = stream.ReadByte();
            if (type != (int) FileType.PTableFile)
                throw new CorruptIndexException("Corrupted PTable.", new InvalidFileException("Wrong type of PTable."));
            var version = stream.ReadByte();
            if (version == -1)
                throw new CorruptIndexException("Couldn't read version of PTable from header.", new InvalidFileException("Invalid PTable file."));
            return new PTableHeader((byte)version);
        }
    }
}