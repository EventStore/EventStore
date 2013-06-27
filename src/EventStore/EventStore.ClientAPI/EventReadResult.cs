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
    public enum EventReadStatus
    {
        Success = 0,
        NotFound = 1,
        NoStream = 2,
        StreamDeleted = 3,
    }

    /// <summary>
    /// A Event Read Result is the result of a single event read operation to the event store.
    /// </summary>
    public class EventReadResult
    {
        /// <summary>
        /// The <see cref="EventReadStatus"/> representing the status of this read attempt
        /// </summary>
        public readonly EventReadStatus Status;

        /// <summary>
        /// The name of the stream read
        /// </summary>
        public readonly string Stream;

        /// <summary>
        /// The event number of the requested event.
        /// </summary>
        public readonly int EventNumber;

        /// <summary>
        /// The event read represented as <see cref="ResolvedEvent"/>
        /// </summary>
        public readonly ResolvedEvent? Event;

        internal EventReadResult(EventReadStatus status, 
                                 string stream, 
                                 int eventNumber, 
                                 ClientMessage.ResolvedIndexedEvent @event)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            Status = status;
            Stream = stream;
            EventNumber = eventNumber;
            Event = status == EventReadStatus.Success ? new ResolvedEvent(@event) : (ResolvedEvent?)null;
        }
    }
}