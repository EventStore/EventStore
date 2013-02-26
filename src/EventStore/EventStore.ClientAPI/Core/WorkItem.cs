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
using EventStore.ClientAPI.ClientOperations;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI.Core
{
    internal class WorkItem
    {
        private static long _seqNumber = -1;

        public readonly long SeqNo;
        public IClientOperation Operation;

        public int Attempt;
        public long LastUpdatedTicks;

        public WorkItem(IClientOperation operation)
        {
            Ensure.NotNull(operation, "operation");
            SeqNo = NextSeqNo();
            Operation = operation;

            Attempt = 0;
            LastUpdatedTicks = DateTime.UtcNow.Ticks;
        }

        private static long NextSeqNo()
        {
            return Interlocked.Increment(ref _seqNumber);
        }

        public override string ToString()
        {
            return string.Format("WorkItem {0}: {1}, attempt: {2}, seqNo: {3}", 
                                 Operation.GetType().Name, 
                                 Operation,
                                 Attempt, 
                                 SeqNo);
        }
    }
}