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
using System.Text;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class CoreProjectionMultiStreamCheckpointManager : CoreProjectionCheckpointManager
    {
        private int _nextStateIndexToRequest;
        private readonly string _projectionStateUpdatesStreamId;

        public CoreProjectionMultiStreamCheckpointManager(
            ICoreProjection coreProjection, IPublisher publisher, Guid projectionCorrelationId,
            RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted> readDispatcher,
            RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted> writeDispatcher,
            ProjectionConfig projectionConfig, string name,
            PositionTagger positionTagger, string projectionStateUpdatesStreamId)
            : base(
                coreProjection, publisher, projectionCorrelationId, readDispatcher, writeDispatcher, projectionConfig, name, positionTagger)
        {
            _projectionStateUpdatesStreamId = projectionStateUpdatesStreamId;
        }

        protected override void BeginWriteCheckpoint(
            CheckpointTag requestedCheckpointPosition, string requestedCheckpointState)
        {
            CheckpointWritten();
        }

        protected override void BeforeBeginLoadState()
        {
            _nextStateIndexToRequest = -1; // from the end
        }

        protected override void RequestLoadState()
        {
            const int recordsToRequest = 10;
            _readDispatcher.Publish(
                new ClientMessage.ReadStreamEventsBackward(
                    Guid.NewGuid(), _readDispatcher.Envelope, _projectionStateUpdatesStreamId, _nextStateIndexToRequest,
                    recordsToRequest, resolveLinks: false), OnLoadStateReadRequestCompleted);
        }

        private void OnLoadStateReadRequestCompleted(ClientMessage.ReadStreamEventsBackwardCompleted message)
        {
            string checkpointData = null;
            CheckpointTag checkpointTag = null;
            if (message.Events.Length > 0)
            {
                EventRecord checkpoint =
                    message.Events.FirstOrDefault(v => v.Event.EventType == "StateUpdated").Event;
                if (checkpoint != null)
                {
                    checkpointData = Encoding.UTF8.GetString(checkpoint.Data);
                    checkpointTag = checkpoint.Metadata.ParseJson<CheckpointTag>();
                }
            }

            if (checkpointTag == null && message.NextEventNumber != -1)
            {
                _nextStateIndexToRequest = message.NextEventNumber;
                RequestLoadState();
                return;
            }
            CheckpointLoaded(checkpointTag, checkpointData);
        }
    }
}
