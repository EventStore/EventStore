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
    public class ResultWriter : IResultWriter
    {
        private readonly IResultEventEmitter _resultEventEmitter;
        private readonly IEmittedEventWriter _coreProjectionCheckpointManager;
        private readonly bool _producesRunningResults;
        private readonly CheckpointTag _zeroCheckpointTag;
        private readonly string _partitionCatalogStreamName;

        public ResultWriter(
            IResultEventEmitter resultEventEmitter, IEmittedEventWriter coreProjectionCheckpointManager,
            bool producesRunningResults, CheckpointTag zeroCheckpointTag, string partitionCatalogStreamName)
        {
            _resultEventEmitter = resultEventEmitter;
            _coreProjectionCheckpointManager = coreProjectionCheckpointManager;
            _producesRunningResults = producesRunningResults;
            _zeroCheckpointTag = zeroCheckpointTag;
            _partitionCatalogStreamName = partitionCatalogStreamName;
        }

        public void WriteEofResult(
            Guid subscriptionId, string partition, string resultBody, CheckpointTag causedBy, Guid causedByGuid,
            string correlationId)
        {
            if (resultBody != null)
                WriteResult(partition, resultBody, causedBy, causedByGuid, correlationId);
        }

        public void WritePartitionMeasured(Guid subscriptionId, string partition, int size)
        {
            // intentionally does nothing
        }

        private void WriteResult(
            string partition, string resultBody, CheckpointTag causedBy, Guid causedByGuid, string correlationId)
        {
            var resultEvents = ResultUpdated(partition, resultBody, causedBy);
            if (resultEvents != null)
                _coreProjectionCheckpointManager.EventsEmitted(resultEvents, causedByGuid, correlationId);
        }

        public void WriteRunningResult(EventProcessedResult result)
        {
            if (!_producesRunningResults)
                return;
            var oldState = result.OldState;
            var newState = result.NewState;
            var resultBody = newState.Result;
            if (oldState.Result != resultBody)
            {
                var partition = result.Partition;
                var causedBy = newState.CausedBy;
                WriteResult(
                    partition, resultBody, causedBy, result.CausedBy, result.CorrelationId);
            }
        }

        private EmittedEventEnvelope[] ResultUpdated(string partition, string result, CheckpointTag causedBy)
        {
            return _resultEventEmitter.ResultUpdated(partition, result, causedBy);
        }

        protected EmittedEventEnvelope[] RegisterNewPartition(string partition, CheckpointTag at)
        {
            return new[]
            {
                new EmittedEventEnvelope(
                    new EmittedDataEvent(
                        _partitionCatalogStreamName, Guid.NewGuid(), "$partition", false, partition,
                        null, at, null))
            };
        }

        public void AccountPartition(EventProcessedResult result)
        {
            if (_producesRunningResults)
                if (result.Partition != "" && result.OldState.CausedBy == _zeroCheckpointTag)
                {
                    var resultEvents = RegisterNewPartition(result.Partition, result.CheckpointTag);
                    if (resultEvents != null)
                        _coreProjectionCheckpointManager.EventsEmitted(
                            resultEvents, Guid.Empty, correlationId: null);
                }
        }

        public void EventsEmitted(
            EmittedEventEnvelope[] scheduledWrites, Guid causedBy, string correlationId)
        {
            _coreProjectionCheckpointManager.EventsEmitted(
                scheduledWrites, causedBy, correlationId);
        }

        public void WriteProgress(Guid subscriptionId, float progress)
        {
            // intentionally does nothing
        }
    }
}
