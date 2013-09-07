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

namespace EventStore.Projections.Core.Services.Processing
{
    public sealed class WriteQueryResultProjectionProcessingPhase : WriteQueryResultProjectionProcessingPhaseBase
    {
        private readonly IEmittedEventWriter _emittedEventWriter;

        public WriteQueryResultProjectionProcessingPhase(
            int phase, string resultStream, ICoreProjectionForProcessingPhase coreProjection,
            PartitionStateCache stateCache, ICoreProjectionCheckpointManager checkpointManager,
            IEmittedEventWriter emittedEventWriter)
            : base(phase, resultStream, coreProjection, stateCache, checkpointManager)
        {
            _emittedEventWriter = emittedEventWriter;
        }

        protected override void WriteResults(CheckpointTag phaseCheckpointTag)
        {
            var items = _stateCache.Enumerate();
            EmittedStream.WriterConfiguration.StreamMetadata streamMetadata = null;
            _emittedEventWriter.EventsEmitted(
                (from item in items
                    let partitionState = item.Item2
                    select
                        new EmittedEventEnvelope(
                            new EmittedDataEvent(
                                _resultStream, Guid.NewGuid(), "Result", true, partitionState.Result, null, phaseCheckpointTag,
                                null), streamMetadata)).ToArray(), Guid.Empty, null);
        }
    }
}
