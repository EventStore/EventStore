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
using EventStore.Core.Messages;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class MultiStreamMultiOutputCheckpointManager : DefaultCheckpointManager
    {
        private readonly IPublisher _publisher;
        private readonly PositionTagger _positionTagger;
        private CheckpointTag _lastOrderCheckpointTag; //TODO: use position tracker to ensure order?
        private EmittedStream _orderStream;

        public MultiStreamMultiOutputCheckpointManager(
            ICoreProjection coreProjection, IPublisher publisher, Guid projectionCorrelationId,
            RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted> readDispatcher,
            RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted> writeDispatcher,
            ProjectionConfig projectionConfig, string name,
            PositionTagger positionTagger, ProjectionNamesBuilder namingBuilder, bool useCheckpoints,
            bool emitStateUpdated, bool emitPartitionCheckpoints = false)
            : base(
                coreProjection, publisher, projectionCorrelationId, readDispatcher, writeDispatcher, projectionConfig, name, positionTagger, namingBuilder, useCheckpoints, emitStateUpdated,
                emitPartitionCheckpoints)
        {
            _publisher = publisher;
            _positionTagger = positionTagger;
        }

        public override void Initialize()
        {
            base.Initialize();
            _lastOrderCheckpointTag = null;
            _orderStream = null;
        }

        public override void Start(CheckpointTag checkpointTag)
        {
            base.Start(checkpointTag);
            _lastOrderCheckpointTag = checkpointTag;
            _orderStream = CreateOrderStream();
            _orderStream.Start();
        }

        public override void RecordEventOrder(
            ProjectionSubscriptionMessage.CommittedEventReceived message, Action committed)
        {
            EnsureStarted();
            if (_stopping)
                throw new InvalidOperationException("Stopping");
            var orderStreamName = _namingBuilder.GetOrderStreamName();
            _orderStream.EmitEvents(
                new[]
                    {
                        new EmittedEvent(
                    orderStreamName, Guid.NewGuid(), "$>",
                    message.PositionSequenceNumber + "@" + message.PositionStreamId, message.CheckpointTag,
                    _lastOrderCheckpointTag, committed)
                    });
            _lastOrderCheckpointTag = message.CheckpointTag;
        }

        private EmittedStream CreateOrderStream()
        {
            return new EmittedStream(
                _namingBuilder.GetOrderStreamName(), _positionTagger.MakeZeroCheckpointTag(), _publisher,
                /* MUST NEVER SEND READY MESSAGE */ this, 100, _logger, noCheckpoints: true);
        }

        public override void GetStatistics(ProjectionStatistics info)
        {
            base.GetStatistics(info);
            info.WritePendingEventsAfterCheckpoint += _orderStream.GetWritePendingEvents();
            info.ReadsInProgress += _orderStream.GetReadsInProgress();
            info.WritesInProgress += _orderStream.GetWritesInProgress();
        }
    }
}
