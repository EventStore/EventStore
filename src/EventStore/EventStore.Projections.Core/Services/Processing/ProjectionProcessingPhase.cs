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
using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ProjectionProcessingPhase
    {
        private readonly CoreProjection _coreProjection;
        private readonly Guid _projectionCorrelationId;
        private readonly CoreProjectionQueue _processingQueue;

        public ProjectionProcessingPhase(
            CoreProjection coreProjection, Guid projectionCorrelationId,
            IPublisher publisher, ProjectionConfig projectionConfig, Action updateStatistics)
        {
            _coreProjection = coreProjection;
            _projectionCorrelationId = projectionCorrelationId;
            var projectionQueue = new CoreProjectionQueue(
                projectionCorrelationId, publisher, projectionConfig.PendingEventsThreshold, updateStatistics);
            _processingQueue = projectionQueue;
        }

        public void Handle(EventReaderSubscriptionMessage.CommittedEventReceived message)
        {
            try
            {
                CheckpointTag eventTag = message.CheckpointTag;
                var committedEventWorkItem = new CommittedEventWorkItem(_coreProjection, message, _coreProjection._statePartitionSelector);
                _processingQueue.EnqueueTask(committedEventWorkItem, eventTag);
            }
            catch (Exception ex)
            {
                _coreProjection.SetFaulted(ex);
            }
        }

        public void Handle(EventReaderSubscriptionMessage.ProgressChanged message)
        {
            try
            {
                var progressWorkItem = new ProgressWorkItem(_coreProjection, _coreProjection._checkpointManager, message.Progress);
                _processingQueue.EnqueueTask(progressWorkItem, message.CheckpointTag, allowCurrentPosition: true);
                _processingQueue.ProcessEvent();
            }
            catch (Exception ex)
            {
                _coreProjection.SetFaulted(ex);
            }
        }

        public void Handle(EventReaderSubscriptionMessage.NotAuthorized message)
        {
            try
            {
                var progressWorkItem = new NotAuthorizedWorkItem(_coreProjection);
                _processingQueue.EnqueueTask(progressWorkItem, message.CheckpointTag, allowCurrentPosition: true);
                _processingQueue.ProcessEvent();
            }
            catch (Exception ex)
            {
                _coreProjection.SetFaulted(ex);
            }
        }

        public void Handle(EventReaderSubscriptionMessage.EofReached message)
        {
            try
            {
                _coreProjection.Unsubscribed();
                var progressWorkItem = new CompletedWorkItem(_coreProjection);
                _processingQueue.EnqueueTask(progressWorkItem, message.CheckpointTag, allowCurrentPosition: true);
                _processingQueue.ProcessEvent();
            }
            catch (Exception ex)
            {
                _coreProjection.SetFaulted(ex);
            }
        }

        public void Handle(EventReaderSubscriptionMessage.CheckpointSuggested message)
        {
            try
            {
                if (_coreProjection.CheckpointStrategy.UseCheckpoints)
                {
                    CheckpointTag checkpointTag = message.CheckpointTag;
                    var checkpointSuggestedWorkItem = new CheckpointSuggestedWorkItem(_coreProjection, message, _coreProjection.CheckpointManager);
                    _processingQueue.EnqueueTask(checkpointSuggestedWorkItem, checkpointTag, allowCurrentPosition: true);
                }
                _processingQueue.ProcessEvent();
            }
            catch (Exception ex)
            {
                _coreProjection.SetFaulted(ex);
            }
        }

        public void Handle(CoreProjectionManagementMessage.GetState message)
        {
            try
            {
                var getStateWorkItem = new GetStateWorkItem(
                    message.Envelope, message.CorrelationId, message.ProjectionId, _coreProjection, _coreProjection.PartitionStateCache, message.Partition);
                _processingQueue.EnqueueOutOfOrderTask(getStateWorkItem);
                _processingQueue.ProcessEvent();
            }
            catch (Exception ex)
            {
                message.Envelope.ReplyWith(
                    new CoreProjectionManagementMessage.StateReport(
                        message.CorrelationId, _coreProjection.ProjectionCorrelationId, message.Partition, state: null, position: null,
                        exception: ex));
                _coreProjection.SetFaulted(ex);
            }
        }

        public void Handle(CoreProjectionManagementMessage.GetResult message)
        {
            try
            {
                var getResultWorkItem = new GetResultWorkItem(
                    message.Envelope, message.CorrelationId, message.ProjectionId, _coreProjection, message.Partition);
                _processingQueue.EnqueueOutOfOrderTask(getResultWorkItem);
                _processingQueue.ProcessEvent();
            }
            catch (Exception ex)
            {
                message.Envelope.ReplyWith(
                    new CoreProjectionManagementMessage.ResultReport(
                        message.CorrelationId, _projectionCorrelationId, message.Partition, result: null, position: null,
                        exception: ex));
                _coreProjection.SetFaulted(ex);
            }
        }

        public void Initialize()
        {
            _processingQueue.Initialize();
        }

        public void ProcessEvent()
        {
            _processingQueue.ProcessEvent();
        }

        public void InitializeFromCheckpoint(CheckpointTag checkpointTag)
        {
            _processingQueue.InitializeQueue(checkpointTag);
        }

        public void Subscribed(Guid subscriptionId)
        {
            _processingQueue.Subscribed(subscriptionId);
        }

        public void Unsubscribed()
        {
            _processingQueue.Unsubscribed();
        }

        public int GetBufferedEventCount()
        {
            return _processingQueue.GetBufferedEventCount();
        }

        public string GetStatus()
        {
            return _processingQueue.GetStatus();
        }
    }
}