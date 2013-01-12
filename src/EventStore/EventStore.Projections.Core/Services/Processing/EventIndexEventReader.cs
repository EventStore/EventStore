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
using System.Collections.Generic;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.TimerService;

namespace EventStore.Projections.Core.Services.Processing
{
    public class EventIndexEventReader : MultiStreamEventReaderBase<EventPosition>
    {
        public EventIndexEventReader(
            IPublisher publisher, Guid distibutionPointCorrelationId, string[] streams,
            Dictionary<string, int> fromPositions, bool resolveLinkTos, ITimeProvider timeProvider,
            bool stopOnEof = false)
            : base(
                publisher, distibutionPointCorrelationId, streams, fromPositions, resolveLinkTos, timeProvider,
                stopOnEof, skipStreamCreated: true)
        {
        }

        protected override EventPosition? EventPairToPosition(EventStore.Core.Data.ResolvedEvent resolvedEvent)
        {
            var @link = resolvedEvent.Link;
            return @link.Metadata.ParseJson<CheckpointTag>().Position;
        }

        protected override EventPosition? MessageToLastCommitPosition(
            ClientMessage.ReadStreamEventsForwardCompleted message)
        {
            var lastCommitPosition = GetLastCommitPositionFrom(message);
            return lastCommitPosition.HasValue ? new EventPosition(message.LastCommitPosition, 0) : (EventPosition?)null;
        }

        protected override EventPosition GetItemPosition(Tuple<EventRecord, EventRecord, float> head)
        {
            return head.Item2.Metadata.ParseJson<CheckpointTag>().Position;
        }

        protected override EventPosition GetMaxPosition()
        {
            return new EventPosition(long.MaxValue, long.MaxValue);
        }

        protected override long? PositionToSafeJoinPosition(EventPosition? safePositionToJoin)
        {
            return safePositionToJoin != null ? safePositionToJoin.GetValueOrDefault().CommitPosition : (long?) null;
        }
    }
}
