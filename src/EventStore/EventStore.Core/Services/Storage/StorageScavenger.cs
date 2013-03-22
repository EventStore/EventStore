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
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Services.Storage
{
    public class StorageScavenger: IHandle<SystemMessage.ScavengeDatabase>
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<StorageScavenger>();

        private readonly TFChunkDb _db;
        private readonly IReadIndex _readIndex;
        private readonly bool _alwaysKeepScavenged;
        private readonly bool _mergeChunks;

        private int _isScavengingRunning;

        public StorageScavenger(TFChunkDb db, IReadIndex readIndex, bool alwaysKeepScavenged, bool mergeChunks)
        {
            Ensure.NotNull(db, "db");
            Ensure.NotNull(readIndex, "readIndex");
            _db = db;
            _readIndex = readIndex;
            _alwaysKeepScavenged = alwaysKeepScavenged;
            _mergeChunks = mergeChunks;
        }

        public void Handle(SystemMessage.ScavengeDatabase message)
        {
            if (Interlocked.CompareExchange(ref _isScavengingRunning, 1, 0) == 0)
            {
                ThreadPool.QueueUserWorkItem(_ => Scavenge());
            }
        }

        private void Scavenge()
        {
            try
            {
                var scavenger = new TFChunkScavenger(_db, _readIndex);
                scavenger.Scavenge(_alwaysKeepScavenged, _mergeChunks);
            }
            catch (Exception exc)
            {
                Log.ErrorException(exc, "SCAVENGING: error while scavenging DB.");
            }

            Interlocked.Exchange(ref _isScavengingRunning, 0);
        }
    }
}