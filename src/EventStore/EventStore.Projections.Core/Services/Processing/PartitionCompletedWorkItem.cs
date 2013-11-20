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
    class PartitionCompletedWorkItem : CheckpointWorkItemBase
    {
        private readonly IEventProcessingProjectionPhase _projection;
        private readonly ICoreProjectionCheckpointManager _checkpointManager;
        private readonly string _partition;
        private readonly CheckpointTag _checkpointTag;
        private PartitionState _state;

        public PartitionCompletedWorkItem(
            IEventProcessingProjectionPhase projection, ICoreProjectionCheckpointManager checkpointManager,
            string partition, CheckpointTag checkpointTag)
            : base()
        {
            _projection = projection;
            _checkpointManager = checkpointManager;
            _partition = partition;
            _checkpointTag = checkpointTag;
        }

        protected override void Load(CheckpointTag checkpointTag)
        {
            if (_partition == null)
                throw new NotSupportedException();
            _projection.BeginGetPartitionStateAt(_partition, _checkpointTag, LoadCompleted, lockLoaded: false);
        }

        private void LoadCompleted(PartitionState obj)
        {
            _state = obj;
            NextStage();
        }

        protected override void WriteOutput()
        {
            _projection.EmitEofResult(_partition, _state.Result, _checkpointTag, Guid.Empty, null);
            //NOTE: write output is an ordered processing stage
            //      thus all the work items before have been already processed
            //      and as we are processing in the stream-by-stream mode
            //      it is safe to clean everything before this position up
            _projection.UnlockAndForgetBefore(_checkpointTag);
            _checkpointManager.PartitionCompleted(_partition);
            NextStage();
        }
    }
}
