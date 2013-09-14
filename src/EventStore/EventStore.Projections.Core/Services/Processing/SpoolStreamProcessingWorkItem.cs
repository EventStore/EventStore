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
        private readonly IResultWriter _resultWriter;
        private readonly EventReaderSubscriptionMessage.CommittedEventReceived _message;

        private readonly
            PublishSubscribeDispatcher
                <ReaderSubscriptionManagement.SpoolStreamReading, ReaderSubscriptionManagement.SpoolStreamReading,
                    PartitionProcessingResult> _spoolProcessingResponseDispatcher;

        private readonly SlaveProjectionCommunicationChannels _slaves;
        private PartitionProcessingResult _resultMessage;
        private Guid _spoolRequestId;

        public SpoolStreamProcessingWorkItem(
            IResultWriter resultWriter,
            EventReaderSubscriptionMessage.CommittedEventReceived message, SlaveProjectionCommunicationChannels slaves,
            PublishSubscribeDispatcher
                <ReaderSubscriptionManagement.SpoolStreamReading, ReaderSubscriptionManagement.SpoolStreamReading,
                    PartitionProcessingResult> spoolProcessingResponseDispatcher)
            : base(Guid.NewGuid())
        {
            _resultWriter = resultWriter;
            _message = message;
            _slaves = slaves;
            _spoolProcessingResponseDispatcher = spoolProcessingResponseDispatcher;
        }

        protected override void ProcessEvent()
        {
            //TODO: implement proper load distribution
            var channelGroup = _slaves.Channels["slave"];
            var channel = channelGroup[0];
            var resolvedEvent = _message.Data;
            var streamId = SystemEventTypes.StreamReferenceEventToStreamId(resolvedEvent.EventType, resolvedEvent.Data);
            _spoolRequestId = _spoolProcessingResponseDispatcher.PublishSubscribe(
                channel.PublishEnvelope,
                new ReaderSubscriptionManagement.SpoolStreamReading(
                    channel.CoreProjectionId, Guid.NewGuid(), streamId, resolvedEvent.PositionSequenceNumber), this);
        }

        protected override void WriteOutput()
        {
            _resultWriter.WriteEofResult(
                _resultMessage.Partition, _resultMessage.Result, _resultMessage.Position, Guid.Empty, null);
            NextStage();
        }

        public void Handle(PartitionProcessingResult message)
        {
            _spoolProcessingResponseDispatcher.Cancel(_spoolRequestId);
            _resultMessage = message;
            NextStage();
        }
    }
}
