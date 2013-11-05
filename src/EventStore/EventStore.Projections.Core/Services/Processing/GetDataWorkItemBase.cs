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
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Services.Processing
{
    abstract class GetDataWorkItemBase : WorkItem
    {
        protected readonly string _partition;
        protected readonly IEnvelope _envelope;
        protected Guid _correlationId;
        protected Guid _projectionId;
        private readonly IProjectionPhaseStateManager _projection;
        private PartitionState _state;
        private CheckpointTag _lastProcessedCheckpointTag;

        protected GetDataWorkItemBase(
            IEnvelope envelope, Guid correlationId, Guid projectionId, IProjectionPhaseStateManager projection, string partition)
            : base(null)
        {
            if (envelope == null) throw new ArgumentNullException("envelope");
            if (partition == null) throw new ArgumentNullException("partition");
            _partition = partition;
            _envelope = envelope;
            _correlationId = correlationId;
            _projectionId = projectionId;
            _projection = projection;
        }

        protected override void GetStatePartition()
        {
            NextStage(_partition);
        }

        protected override void Load(CheckpointTag checkpointTag)
        {
            _lastProcessedCheckpointTag = _projection.LastProcessedEventPosition;
            _projection.BeginGetPartitionStateAt(
                _partition, _lastProcessedCheckpointTag, LoadCompleted, lockLoaded: false);
        }

        private void LoadCompleted(PartitionState state)
        {
            _state = state;
            NextStage();
        }

        protected override void WriteOutput()
        {
            Reply(_state, _lastProcessedCheckpointTag);
            NextStage();
        }

        protected abstract void Reply(PartitionState state, CheckpointTag checkpointTag);
    }
}
