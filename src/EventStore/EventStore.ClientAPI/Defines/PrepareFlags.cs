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

namespace EventStore.ClientAPI.Defines
{
    [Flags]
    public enum PrepareFlags : ushort
    {
        None = 0x00,
        Data = 0x01,                // prepare contains data
        TransactionBegin = 0x02,    // prepare starts transaction
        TransactionEnd = 0x04,      // prepare ends transaction
        StreamDelete = 0x08,        // prepare deletes stream

        IsCommited = 0x10,          // prepare should be considered committed immediately, no commit will follow in TF
        //Snapshot = 0x20,          // prepare belongs to snapshot stream, only last event in stream will be kept after scavenging

        //Update = 0x80,            // prepare updates previous instance of the same event, DANGEROUS!
        IsJson = 0x100,             // indicates data & metadata are valid json

        // aggregate flag set
        DeleteTombstone = TransactionBegin | TransactionEnd | StreamDelete,
        SingleWrite = Data | TransactionBegin | TransactionEnd
    }
}