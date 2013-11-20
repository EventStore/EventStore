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

using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Messages;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// An Stream Events Slice represents the result of a single read operation to the event store.
    /// </summary>
    public class StreamEventsSlice
    {
        /// <summary>
        /// The <see cref="SliceReadStatus"/> representing the status of this read attempt
        /// </summary>
        public readonly SliceReadStatus Status;

        /// <summary>
        /// The name of the stream read
        /// </summary>
        public readonly string Stream;

        /// <summary>
        /// The starting point (represented as a sequence number) of the read operation.
        /// </summary>
        public readonly int FromEventNumber;

        /// <summary>
        /// The direction of read request.
        /// </summary>
        public readonly ReadDirection ReadDirection;

        /// <summary>
        /// The events read represented as <see cref="RecordedEvent"/>
        /// </summary>
        public readonly ResolvedEvent[] Events;

        /// <summary>
        /// The next event number that can be read.
        /// </summary>
        public readonly int NextEventNumber;

        /// <summary>
        /// The last event number in the stream.
        /// </summary>
        public readonly int LastEventNumber;

        /// <summary>
        /// A boolean representing whether or not this is the end of the stream.
        /// </summary>
        public readonly bool IsEndOfStream;

        internal StreamEventsSlice(SliceReadStatus status, 
                                   string stream, 
                                   int fromEventNumber, 
                                   ReadDirection readDirection,
                                   ClientMessage.ResolvedIndexedEvent[] events,
                                   int nextEventNumber,
                                   int lastEventNumber,
                                   bool isEndOfStream)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            Status = status;
            Stream = stream;
            FromEventNumber = fromEventNumber;
            ReadDirection = readDirection;
            if (events == null || events.Length == 0)
                Events = Empty.ResolvedEvents;
            else
            {
                Events = new ResolvedEvent[events.Length];
                for (int i = 0; i < Events.Length; ++i)
                {
                    Events[i] = new ResolvedEvent(events[i]);
                }
            }
            NextEventNumber = nextEventNumber;
            LastEventNumber = lastEventNumber;
            IsEndOfStream = isEndOfStream;
        }
    }
}