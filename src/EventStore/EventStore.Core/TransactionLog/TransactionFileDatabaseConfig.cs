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
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.TransactionLog.Checkpoint;

namespace EventStore.Core.TransactionLog
{
    public class TransactionFileDatabaseConfig
    {
        private readonly IDictionary<string, ICheckpoint> _namedCheckpoints;

        public readonly string Path;
        public readonly string FilePrefix;
        public readonly long SegmentSize;
        public readonly ICheckpoint WriterCheckpoint;
        public readonly IFileNamingStrategy FileNamingStrategy;

        public IEnumerable<ICheckpoint> Checkpoints { get { return _namedCheckpoints.Values; } }

        //TODO GFY add fluent builder for this
        public TransactionFileDatabaseConfig(string path,
                                             string filePrefix, 
                                             long segmentSize, 
                                             ICheckpoint writerCheckpoint, 
                                             IEnumerable<ICheckpoint> namedCheckpoints)
        {
            if (path == null) 
                throw new ArgumentNullException("path");
            if (filePrefix == null) 
                throw new ArgumentNullException("filePrefix");
            if (segmentSize <= 0)
                throw new ArgumentOutOfRangeException("segmentSize");
            if (writerCheckpoint == null)
                throw new ArgumentNullException("writerCheckpoint");
            if (namedCheckpoints == null) 
                throw new ArgumentNullException("namedCheckpoints");

//            if ((segmentSize & (segmentSize-1)) != 0)
//                throw new ArgumentException("Segment size should be the power of 2.", "segmentSize");
            
            Path = path;
            FilePrefix = filePrefix;
            FileNamingStrategy = new PrefixFileNamingStrategy(path, filePrefix);
            SegmentSize = segmentSize;
            WriterCheckpoint = writerCheckpoint;
            _namedCheckpoints = namedCheckpoints.ToDictionary(x => x.Name);
        }

        public ICheckpoint GetNamedCheckpoint(string name)
        {
            ICheckpoint item;
            _namedCheckpoints.TryGetValue(name, out item);
            return item;
        }
    }
}