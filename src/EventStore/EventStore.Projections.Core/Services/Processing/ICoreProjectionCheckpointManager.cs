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
    public interface ICoreProjectionCheckpointReader
    {
        void BeginLoadState();
        void Initialize();
    }

    public interface IEmittedEventWriter
    {
        void EventsEmitted(EmittedEventEnvelope[] scheduledWrites, Guid causedBy, string correlationId);
    }

    public interface ICoreProjectionCheckpointManager
    {
        void Initialize();
        void Start(CheckpointTag checkpointTag);
        void Stopping();
        void Stopped();
        void GetStatistics(ProjectionStatistics info);


        void StateUpdated(string partition, PartitionState oldState, PartitionState newState);
        void PartitionCompleted(string partition);
        void EventProcessed(CheckpointTag checkpointTag, float progress);

        /// <summary>
        /// Suggests a checkpoint which may complete immediately or be delayed
        /// </summary>
        /// <param name="checkpointTag"></param>
        /// <param name="progress"></param>
        /// <returns>true - if checkpoint has been completed (or skipped)</returns>
        bool CheckpointSuggested(CheckpointTag checkpointTag, float progress);
        void Progress(float progress);

        void BeginLoadPrerecordedEvents(CheckpointTag checkpointTag);

        void BeginLoadPartitionStateAt(
            string statePartition, CheckpointTag requestedStateCheckpointTag,
            Action<PartitionState> loadCompleted);

        void RecordEventOrder(ResolvedEvent resolvedEvent, CheckpointTag orderCheckpointTag, Action committed);
        CheckpointTag LastProcessedEventPosition { get; }
    }
}
