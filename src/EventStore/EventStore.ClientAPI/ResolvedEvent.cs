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

using EventStore.ClientAPI.Messages;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// A structure representing a single event or an resolved link event.
    /// </summary>
    public struct ResolvedEvent
    {
        /// <summary>
        /// The event, or the resolved link event if this <see cref="ResolvedEvent"/> is
        /// a link event.
        /// </summary>
        public readonly RecordedEvent Event;
        /// <summary>
        /// The link event if this <see cref="ResolvedEvent"/> is a link event.
        /// </summary>
        public readonly RecordedEvent Link;
        /// <summary>
        /// Returns the event that was read or which triggered the subscription.
        /// 
        /// If this <see cref="ResolvedEvent"/> represents a link event, the Link
        /// will be the <see cref="OriginalEvent"/>, otherwise it will be the
        /// Event.
        /// </summary>
        public RecordedEvent OriginalEvent { get { return Link ?? Event; } }
        /// <summary>
        /// Indicates whether this <see cref="ResolvedEvent"/> is a resolved link
        /// event.
        /// </summary>
        public bool IsResolved { get { return Link != null; } }

        /// <summary>
        /// The logical position of the <see cref="OriginalEvent"/>.
        /// </summary>
        public readonly Position? OriginalPosition;
        /// <summary>
        /// The stream name of the <see cref="OriginalEvent" />.
        /// </summary>
        public string OriginalStreamId { get { return OriginalEvent.EventStreamId; } }
        /// <summary>
        /// The event number in the stream of the <see cref="OriginalEvent"/>.
        /// </summary>
        public int OriginalEventNumber { get { return OriginalEvent.EventNumber; } }

        internal ResolvedEvent(ClientMessage.ResolvedEvent evnt)
        {
            Event = new RecordedEvent(evnt.Event);
            Link = evnt.Link == null ? null : new RecordedEvent(evnt.Link);
            OriginalPosition = new Position(evnt.CommitPosition, evnt.PreparePosition);
        }

        internal ResolvedEvent(ClientMessage.ResolvedIndexedEvent evnt)
        {
            Event = new RecordedEvent(evnt.Event);
            Link = evnt.Link == null ? null : new RecordedEvent(evnt.Link);
            OriginalPosition = null;
        }
    }
}