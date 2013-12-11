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
using EventStore.Core.Services;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class SpoolStreamProcessingWorkItem : WorkItem, IHandle<PartitionProcessingResult>
    {
        private readonly ISpoolStreamWorkItemContainer _container;
        private readonly IResultWriter _resultWriter;
        private readonly ParallelProcessingLoadBalancer _loadBalancer;
        private readonly EventReaderSubscriptionMessage.CommittedEventReceived _message;

        private readonly SpooledStreamReadingDispatcher _spoolProcessingResponseDispatcher;

        private readonly SlaveProjectionCommunicationChannels _slaves;
        private PartitionProcessingResult _resultMessage;
        private Tuple<Guid, string> _spoolRequestId;
        private readonly long _limitingCommitPosition;
        private readonly Guid _subscriptionId;
        private readonly Guid _correlationId;
        private readonly bool _definesCatalogTransform;

        public SpoolStreamProcessingWorkItem(
            ISpoolStreamWorkItemContainer container, IResultWriter resultWriter,
            ParallelProcessingLoadBalancer loadBalancer, EventReaderSubscriptionMessage.CommittedEventReceived message,
            SlaveProjectionCommunicationChannels slaves,
            SpooledStreamReadingDispatcher spoolProcessingResponseDispatcher, long limitingCommitPosition,
            Guid subscriptionId, Guid correlationId, bool definesCatalogTransform)
            : base(Guid.NewGuid())
        {
            if (resultWriter == null) throw new ArgumentNullException("resultWriter");
            if (slaves == null) throw new ArgumentNullException("slaves");
            if (spoolProcessingResponseDispatcher == null)
                throw new ArgumentNullException("spoolProcessingResponseDispatcher");
            _container = container;
            _resultWriter = resultWriter;
            _loadBalancer = loadBalancer;
            _message = message;
            _slaves = slaves;
            _spoolProcessingResponseDispatcher = spoolProcessingResponseDispatcher;
            _limitingCommitPosition = limitingCommitPosition;
            _subscriptionId = subscriptionId;
            _correlationId = correlationId;
            _definesCatalogTransform = definesCatalogTransform;
        }

        protected override void ProcessEvent()
        {
            var channelGroup = _slaves.Channels["slave"];
            var resolvedEvent = _message.Data;
            var position = _message.CheckpointTag;

            var streamId = TransformCatalogEvent(position, resolvedEvent);
            _loadBalancer.ScheduleTask(
                streamId, (streamId_, workerIndex) =>
                {
                    var channel = channelGroup[workerIndex];
                    _spoolRequestId = _spoolProcessingResponseDispatcher.PublishSubscribe(
                        channel.PublishEnvelope,
                        new ReaderSubscriptionManagement.SpoolStreamReading(
                            channel.SubscriptionId, _correlationId, streamId_, resolvedEvent.PositionSequenceNumber,
                            _limitingCommitPosition), this);
                });
        }

        private string TransformCatalogEvent(CheckpointTag position, ResolvedEvent resolvedEvent)
        {
            if (_definesCatalogTransform)
                return _container.TransformCatalogEvent(position, resolvedEvent);
            return SystemEventTypes.StreamReferenceEventToStreamId(
                resolvedEvent.EventType, resolvedEvent.Data);
        }

        protected override void WriteOutput()
        {
            _resultWriter.WriteEofResult(_subscriptionId, 
                _resultMessage.Partition, _resultMessage.Result, _resultMessage.Position, Guid.Empty, null);
            NextStage();
        }

        public void Handle(PartitionProcessingResult message)
        {
            _loadBalancer.AccountCompleted(message.Partition);
            _spoolProcessingResponseDispatcher.Cancel(_spoolRequestId);
            _resultMessage = message;
            _container.CompleteSpoolProcessingWorkItem(_correlationId);
            NextStage();
        }
    }

    public interface ISpoolStreamWorkItemContainer
    {
        string TransformCatalogEvent(CheckpointTag position, ResolvedEvent @event);
        void CompleteSpoolProcessingWorkItem(Guid subscriptionId);
    }
}
