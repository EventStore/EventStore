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
namespace EventStore.Core.TransactionLog.Chunks
{
    public static class TFConsts
    {
        public const int MaxLogRecordSize = 16 * 1024 * 1024; // 16Mb ought to be enough for everything?.. ;)

        public const int MidpointsDepth = 10;

        public const int StorageReaderHandlerCount = 4;

        public const int ReadIndexReaderCount = 1 /*StorageWriter*/
                                                + 1 /*StorageChaser*/
                                                + 1 /*Projections*/
                                                + 1 /*Scavenging*/
                                                + StorageReaderHandlerCount;

        public const int TFChunkReaderCount = ReadIndexReaderCount + 2 /* for caching/uncaching, populating midpoints */;

        public const int ChunkSize = 256 * 1024 * 1024;
        public const int CachedChunkCount = 2;

        public const int MemTableEntryCount = 1000000;
        public const int MetadataCacheCapacity = 100000;
    }
}