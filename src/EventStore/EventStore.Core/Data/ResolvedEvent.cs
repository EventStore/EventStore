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
namespace EventStore.Core.Data
{
    public struct ResolvedEvent
    {
        public static readonly ResolvedEvent[] EmptyArray = new ResolvedEvent[0];

        public readonly EventRecord Event;
        public readonly EventRecord Link;
        public EventRecord OriginalEvent { get { return Link ?? Event; } }

        /// <summary>
        /// Position of the OriginalEvent (unresolved link or event) if available
        /// </summary>
        public readonly TFPos? OriginalPosition;

        public readonly ReadEventResult ResolveResult;

        public string OriginalStreamId { get { return OriginalEvent.EventStreamId; } }
        public int OriginalEventNumber { get { return OriginalEvent.EventNumber; } }

        public ResolvedEvent(
            EventRecord @event, EventRecord link, ReadEventResult resolveResult = default(ReadEventResult))
        {
            Event = @event;
            Link = link;
            OriginalPosition = null;
            ResolveResult = resolveResult;
        }

        public ResolvedEvent(
            EventRecord @event, EventRecord link, long commitPosition,
            ReadEventResult resolveResult = default(ReadEventResult))
        {
            Event = @event;
            Link = link;
            OriginalPosition = new TFPos(commitPosition, (link ?? @event).LogPosition);
            ResolveResult = resolveResult;
        }

        public ResolvedEvent(EventRecord @event)
        {
            Event = @event;
            Link = null;
            OriginalPosition = null;
            ResolveResult = default(ReadEventResult);
        }

        public ResolvedEvent(EventRecord @event, long commitPosition)
        {
            Event = @event;
            Link = null;
            OriginalPosition = new TFPos(commitPosition, @event.LogPosition);
            ResolveResult = default(ReadEventResult);
        }
    }
}