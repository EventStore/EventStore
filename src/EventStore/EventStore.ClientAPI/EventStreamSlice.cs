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

using System.Collections.Generic;
using EventStore.ClientAPI.Common.Utils;
using System.Linq;
using EventStore.ClientAPI.Messages;

namespace EventStore.ClientAPI
{
    public class EventStreamSlice
    {
        internal static readonly RecordedEvent[] EmptyEvents = new RecordedEvent[0];

        public readonly SliceReadStatus Status;
        public readonly string Stream;

        public readonly int Start;
        public readonly int Count;
        public readonly RecordedEvent[] Events;

        public readonly int NextEventNumber;
        public readonly int LastEventNumber;

        public readonly bool IsEndOfStream;

        internal EventStreamSlice(SliceReadStatus status, 
                                  string stream, 
                                  int start, 
                                  int count, 
                                  IEnumerable<ClientMessage.EventLinkPair> events,
                                  int nextEventNumber,
                                  int lastEventNumber,
                                  bool isEndOfStream)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            Status = status;
            Stream = stream;
            Start = start;
            Count = count;
            Events = events == null ? EmptyEvents : events.Select(e => new RecordedEvent(e.Event)).ToArray();
            NextEventNumber = nextEventNumber;
            LastEventNumber = lastEventNumber;
            IsEndOfStream = isEndOfStream;
        }
    }
}