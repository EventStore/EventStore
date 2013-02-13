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
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    class CommittedEventWorkItem : WorkItem
    {
        private readonly ProjectionSubscriptionMessage.CommittedEventReceived _message;
        private string _partition;
        private readonly StatePartitionSelector _statePartitionSelector;
        private EventProcessedResult _eventProcessedResult;

        public CommittedEventWorkItem(
            CoreProjection projection, ProjectionSubscriptionMessage.CommittedEventReceived message,
            StatePartitionSelector statePartitionSelector)
            : base(projection, Guid.NewGuid())
        {
            _statePartitionSelector = statePartitionSelector;
            _message = message;
        }

        protected override void RecordEventOrder()
        {
            Projection.RecordEventOrder(_message, () => NextStage());
        }

        protected override void GetStatePartition()
        {
            _partition = _statePartitionSelector.GetStatePartition(_message);
            if (_partition == null)
                // skip processing of events not mapped to any partition
                Complete();
            else
                NextStage(_partition);
        }

        protected override void Load(CheckpointTag checkpointTag)
        {
            // we load partition state even if stopping etc.  should we skip?
            Projection.BeginGetPartitionStateAt(
                _partition, _message.CheckpointTag, LoadCompleted, lockLoaded: true);
        }

        private void LoadCompleted(PartitionState state)
        {
            NextStage();
        }

        protected override void ProcessEvent()
        {
            var eventProcessedResult = Projection.ProcessCommittedEvent(_message, _partition);
            if (eventProcessedResult != null)
                SetEventProcessedResult(eventProcessedResult);
            NextStage();
        }

        protected override void WriteOutput()
        {
            Projection.FinalizeEventProcessing(_eventProcessedResult, _message.CheckpointTag, _message.Progress);
            NextStage();
        }

        private void SetEventProcessedResult(EventProcessedResult eventProcessedResult)
        {
            _eventProcessedResult = eventProcessedResult;
        }
    }
}
