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
    public sealed class WriteQueryEofProjectionProcessingPhase : IProjectionProcessingPhase
    {
        private readonly int _phase;
        private readonly string _resultStream;
        private readonly ICoreProjectionForProcessingPhase _coreProjection;
        private readonly PartitionStateCache _stateCache;
        private readonly ICoreProjectionCheckpointManager _checkpointManager;

        private bool _subscribed;
        private PhaseState _projectionState;

        public WriteQueryEofProjectionProcessingPhase(
            int phase, string resultStream, ICoreProjectionForProcessingPhase coreProjection,
            PartitionStateCache stateCache, ICoreProjectionCheckpointManager checkpointManager)
        {
            if (resultStream == null) throw new ArgumentNullException("resultStream");
            if (coreProjection == null) throw new ArgumentNullException("coreProjection");
            if (stateCache == null) throw new ArgumentNullException("stateCache");
            if (checkpointManager == null) throw new ArgumentNullException("checkpointManager");
            if (string.IsNullOrEmpty(resultStream)) throw new ArgumentException("resultStream");

            _phase = phase;
            _resultStream = resultStream;
            _coreProjection = coreProjection;
            _stateCache = stateCache;
            _checkpointManager = checkpointManager;
        }

        public void Dispose()
        {
        }

        public void Handle(CoreProjectionManagementMessage.GetState message)
        {
            var state = _stateCache.TryGetPartitionState(message.Partition);
            var stateString = state != null ? state.State : null;
            message.Envelope.ReplyWith(
                new CoreProjectionManagementMessage.StateReport(
                    message.CorrelationId, message.CorrelationId, message.Partition, state: stateString, position: null));
        }

        public void Handle(CoreProjectionManagementMessage.GetResult message)
        {
            var state = _stateCache.TryGetPartitionState(message.Partition);
            var resultString = state != null ? state.Result : null;
            message.Envelope.ReplyWith(
                new CoreProjectionManagementMessage.ResultReport(
                    message.CorrelationId, message.CorrelationId, message.Partition, result: resultString,
                    position: null));
        }

        public void Handle(CoreProjectionProcessingMessage.PrerecordedEventsLoaded message)
        {
            throw new NotImplementedException();
        }

        public CheckpointTag AdjustTag(CheckpointTag tag)
        {
            return tag;
        }

        public void InitializeFromCheckpoint(CheckpointTag checkpointTag)
        {
            _subscribed = false;
        }

        public void ProcessEvent()
        {
            if (!_subscribed)
                throw new InvalidOperationException();
            if (_projectionState != PhaseState.Running)
                return;

            var phaseCheckpointTag = CheckpointTag.FromPhase(_phase, completed: true);
            _checkpointManager.EventProcessed(phaseCheckpointTag, 100.0f);
            _coreProjection.CompletePhase();
        }

        public void Subscribe(CheckpointTag from, bool fromCheckpoint)
        {
            _subscribed = true;
            _coreProjection.Subscribed();
        }

        public void SetProjectionState(PhaseState state)
        {
            _projectionState = state;
        }

        public void GetStatistics(ProjectionStatistics info)
        {
            info.Status = info.Status + "/Writing results";
        }

        public CheckpointTag MakeZeroCheckpointTag()
        {
            return CheckpointTag.FromPhase(_phase, completed: false);
        }

        public ICoreProjectionCheckpointManager CheckpointManager
        {
            get { return _checkpointManager; }
        }

        public void EnsureUnsubscribed()
        {
        }
    }
}
