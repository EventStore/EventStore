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
using System.Linq;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public sealed class WriteQueryResultProjectionProcessingPhase : IProjectionProcessingPhase
    {
        private readonly int _phase;
        private readonly string _resultStream;
        private readonly PartitionStateCache _stateCache;
        private readonly ICoreProjectionCheckpointManager _checkpointManager;

        public WriteQueryResultProjectionProcessingPhase(
            int phase, string resultStream, PartitionStateCache stateCache,
            ICoreProjectionCheckpointManager checkpointManager)
        {
            if (resultStream == null) throw new ArgumentNullException("resultStream");
            if (stateCache == null) throw new ArgumentNullException("stateCache");
            if (checkpointManager == null) throw new ArgumentNullException("checkpointManager");
            if (string.IsNullOrEmpty(resultStream)) throw new ArgumentException("resultStream");

            _phase = phase;
            _resultStream = resultStream;
            _stateCache = stateCache;
            _checkpointManager = checkpointManager;
        }

        public void Dispose()
        {
        }

        public void Handle(CoreProjectionManagementMessage.GetState message)
        {
            throw new NotImplementedException();
        }

        public void Handle(CoreProjectionManagementMessage.GetResult message)
        {
            throw new NotImplementedException();
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
        }

        public void ProcessEvent()
        {
            throw new InvalidOperationException();
        }

        public void Subscribe(CheckpointTag from, bool fromCheckpoint)
        {
            var items = _stateCache.Enumerate();
            EmittedStream.WriterConfiguration.StreamMetadata streamMetadata = null;
            var phaseCheckpointTag = CheckpointTag.FromPhase(_phase, completed: true);
            _checkpointManager.EventsEmitted(
                (from item in items
                    let partitionState = item.Item2
                    select
                        new EmittedEventEnvelope(new EmittedDataEvent(
                            _resultStream, Guid.NewGuid(), "Result", partitionState.Result, null, phaseCheckpointTag,
                            phaseCheckpointTag), streamMetadata)).ToArray(), Guid.Empty, null);
            _checkpointManager.EventProcessed(phaseCheckpointTag, 100.0f);
            _checkpointManager.CheckpointSuggested(phaseCheckpointTag, 100.0f);
        }

        public void SetProjectionState(PhaseState state)
        {
            throw new NotImplementedException();
        }

        public void GetStatistics(ProjectionStatistics info)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
    }
}
