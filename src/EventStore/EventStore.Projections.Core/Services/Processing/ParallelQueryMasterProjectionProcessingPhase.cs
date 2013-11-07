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
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ParallelQueryMasterProjectionProcessingPhase : EventSubscriptionBasedProjectionProcessingPhase,
        IHandle<EventReaderSubscriptionMessage.CommittedEventReceived>

    {
        //TODO: make it configurable
        public const int _maxScheduledSizePerWorker = 10000;
        public const int _maxUnmeasuredTasksPerWorker = 30;

        private readonly SpooledStreamReadingDispatcher _spoolProcessingResponseDispatcher;
        private ParallelProcessingLoadBalancer _loadBalancer;

        private SlaveProjectionCommunicationChannels _slaves;

        public ParallelQueryMasterProjectionProcessingPhase(
            CoreProjection coreProjection, Guid projectionCorrelationId, IPublisher publisher,
            ProjectionConfig projectionConfig, Action updateStatistics, PartitionStateCache partitionStateCache,
            string name, ILogger logger, CheckpointTag zeroCheckpointTag,
            ICoreProjectionCheckpointManager checkpointManager, ReaderSubscriptionDispatcher subscriptionDispatcher,
            IReaderStrategy readerStrategy, IResultWriter resultWriter, bool checkpointsEnabled, bool stopOnEof,
            SpooledStreamReadingDispatcher spoolProcessingResponseDispatcher)
            : base(
                publisher, coreProjection, projectionCorrelationId, checkpointManager, projectionConfig, name, logger,
                zeroCheckpointTag, partitionStateCache, resultWriter, updateStatistics, subscriptionDispatcher,
                readerStrategy, checkpointsEnabled, stopOnEof, orderedPartitionProcessing: true, isBiState: false)
        {
            _spoolProcessingResponseDispatcher = spoolProcessingResponseDispatcher;
        }


        public override void NewCheckpointStarted(CheckpointTag at)
        {
        }

        public override void Dispose()
        {
        }

        public override void AssignSlaves(SlaveProjectionCommunicationChannels slaveProjections)
        {
            _slaves = slaveProjections;
            _loadBalancer = new ParallelProcessingLoadBalancer(
                _slaves.Channels["slave"].Length, _maxScheduledSizePerWorker, _maxUnmeasuredTasksPerWorker);
        }

        public override void Subscribe(CheckpointTag @from, bool fromCheckpoint)
        {
            if (_slaves == null)
                throw new InvalidOperationException("Cannot subscribe to event reader without assigned slave projections");
            base.Subscribe(@from, fromCheckpoint);
        }

        public void Handle(EventReaderSubscriptionMessage.CommittedEventReceived message)
        {
            //TODO:  make sure this is no longer required : if (_state != State.StateLoaded)
            if (IsOutOfOrderSubscriptionMessage(message))
                return;
            RegisterSubscriptionMessage(message);
            try
            {
                var eventTag = message.CheckpointTag;
                var committedEventWorkItem = new SpoolStreamProcessingWorkItem(
                    _resultWriter, _loadBalancer, message, _slaves, _spoolProcessingResponseDispatcher,
                    _subscriptionStartedAtLastCommitPosition, _currentSubscriptionId);
                _processingQueue.EnqueueTask(committedEventWorkItem, eventTag);
                if (_state == PhaseState.Running) // prevent processing mostly one projection
                    EnsureTickPending();
            }
            catch (Exception ex)
            {
                _coreProjection.SetFaulted(ex);
            }

        }

    }
}
