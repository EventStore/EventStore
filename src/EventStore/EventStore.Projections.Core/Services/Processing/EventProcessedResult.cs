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

namespace EventStore.Projections.Core.Services.Processing
{
    public class EventProcessedResult
    {
        private readonly EmittedEventEnvelope[] _emittedEvents;
        private readonly PartitionState _oldState;
        private readonly PartitionState _newState;
        private readonly PartitionState _oldSharedState;
        private readonly PartitionState _newSharedState;
        private readonly string _partition;
        private readonly CheckpointTag _checkpointTag;
        private readonly Guid _causedBy;
        private readonly string _correlationId;

        public EventProcessedResult(
            string partition, CheckpointTag checkpointTag, PartitionState oldState, PartitionState newState,
            PartitionState oldSharedState, PartitionState newSharedState, EmittedEventEnvelope[] emittedEvents,
            Guid causedBy, string correlationId)
        {
            if (partition == null) throw new ArgumentNullException("partition");
            if (checkpointTag == null) throw new ArgumentNullException("checkpointTag");
            _emittedEvents = emittedEvents;
            _causedBy = causedBy;
            _correlationId = correlationId;
            _oldState = oldState;
            _newState = newState;
            _oldSharedState = oldSharedState;
            _newSharedState = newSharedState;
            _partition = partition;
            _checkpointTag = checkpointTag;
        }

        public EmittedEventEnvelope[] EmittedEvents
        {
            get { return _emittedEvents; }
        }

        public PartitionState OldState
        {
            get { return _oldState; }
        }

        /// <summary>
        /// null - means no state change
        /// </summary>
        public PartitionState NewState
        {
            get { return _newState; }
        }

        public PartitionState OldSharedState
        {
            get { return _oldSharedState; }
        }

        /// <summary>
        /// null - means no state change
        /// </summary>
        public PartitionState NewSharedState
        {
            get { return _newSharedState; }
        }

        public string Partition
        {
            get { return _partition; }
        }

        public CheckpointTag CheckpointTag
        {
            get { return _checkpointTag; }
        }

        public Guid CausedBy
        {
            get { return _causedBy; }
        }

        public string CorrelationId
        {
            get { return _correlationId; }
        }
    }
}


