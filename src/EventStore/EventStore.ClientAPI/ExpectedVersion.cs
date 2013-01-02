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
namespace EventStore.ClientAPI
{
    /// <summary>
    /// Constants used for expected version control
    /// </summary>
    /// <remarks>
    /// The use of expected version can be a bit tricky especially when discussing idempotency assurances given by the event store.
    /// 
    /// There are four possible values that can be used for the passing of an expected version.
    /// 
    /// ExpectedVersion.Any (-2) says that you should not conflict with anything.
    /// ExpectedVersion.NoStream (-1) says that the stream should not exist when doing your write.
    /// ExpectedVersion.EmptyStream (0) says the stream should exist but be empty when doing the write.
    /// 
    /// Any other value states that the last event written to the stream should have a sequence number matching your 
    /// expected value.
    /// 
    /// The Event Store will assure idempotency for all operations using any value in ExpectedVersion except for
    /// ExpectedVersion.Any. When using ExpectedVersion.Any the Event Store will do its best to assure idempotency but
    /// will not gaurantee idempotency.
    /// </remarks>
    public static class ExpectedVersion
    {
        /// <summary>
        /// This write should not conflict with anything and should always succeed.
        /// </summary>
        public const int Any = -2;
        /// <summary>
        /// The stream being written to should not yet exist. If it does exist treat that as a concurrency problem
        /// </summary>
        public const int NoStream = -1;
        /// <summary>
        /// The stream should exist and should be empty. If it does not exist or is not empty treat that as a concurrency problem.
        /// </summary>
        public const int EmptyStream = 0;
    }
}