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
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    class GetStateWorkItem : WorkItem
    {
        private readonly string _partition;
        private readonly IEnvelope _envelope;
        private readonly Guid _projectionId;
        private readonly PartitionStateCache _partitionStateCache;

        public GetStateWorkItem(IEnvelope envelope, 
                                Guid projectionId, 
                                CoreProjection projection, 
                                PartitionStateCache partitionStateCache,
                                string partition)
            : base(projection, string.Empty)
        {
            if (envelope == null) throw new ArgumentNullException("envelope");
            if (partitionStateCache == null) throw new ArgumentNullException("partitionStateCache");
            if (partition == null) throw new ArgumentNullException("partition");
            _partition = partition;
            _envelope = envelope;
            _projectionId = projectionId;
            _partitionStateCache = partitionStateCache;
        }

        protected override void Load(CheckpointTag checkpointTag)
        {
            Projection.BeginStatePartitionLoad(_partition, checkpointTag, LoadCompleted);
        }

        private void LoadCompleted()
        {
            var projectionState = _partitionStateCache.GetLockedPartitionState(_partition).Data;
            _envelope.ReplyWith(
                new ProjectionMessage.Projections.Management.StateReport(_projectionId, _partition, projectionState));
            NextStage();
        }
    }
}
