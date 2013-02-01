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
using System.Collections.Generic;
using System.Text;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class MultiStreamMultiOutputCheckpointManager : DefaultCheckpointManager
    {
        private readonly PositionTagger _positionTagger;
        private CheckpointTag _lastOrderCheckpointTag; //TODO: use position tracker to ensure order?
        private EmittedStream _orderStream;
        private bool _orderStreamReadingCompleted;
        private int _loadingItemsCount;
        private readonly Stack<Item> _loadQueue = new Stack<Item>();
        private CheckpointTag _loadingPrerecordedEventsFrom;

        public MultiStreamMultiOutputCheckpointManager(
            ICoreProjection coreProjection, IPublisher publisher, Guid projectionCorrelationId,
            RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted> readDispatcher,
            RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted> writeDispatcher,
            ProjectionConfig projectionConfig, string name, PositionTagger positionTagger,
            ProjectionNamesBuilder namingBuilder, bool useCheckpoints, bool emitStateUpdated,
            bool emitPartitionCheckpoints = false)
            : base(
                coreProjection, publisher, projectionCorrelationId, readDispatcher, writeDispatcher, projectionConfig,
                name, positionTagger, namingBuilder, useCheckpoints, emitStateUpdated, emitPartitionCheckpoints)
        {
            _positionTagger = positionTagger;
        }

        public override void Initialize()
        {
            base.Initialize();
            _lastOrderCheckpointTag = null;
            if (_orderStream != null) _orderStream.Dispose();
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
                _namingBuilder.GetOrderStreamName(), _positionTagger.MakeZeroCheckpointTag(),
                _readDispatcher, _writeDispatcher, /* MUST NEVER SEND READY MESSAGE */ this, 100, _logger,
                noCheckpoints: true);
        }

        public override void GetStatistics(ProjectionStatistics info)
        {
            base.GetStatistics(info);
            if (_orderStream != null)
            {
                info.WritePendingEventsAfterCheckpoint += _orderStream.GetWritePendingEvents();
                info.ReadsInProgress += _orderStream.GetReadsInProgress();
                info.WritesInProgress += _orderStream.GetWritesInProgress();
            }
        }


        protected override void BeginLoadPrerecordedEvents(CheckpointTag checkpointTag)
        {
            BeginLoadPrerecordedEventsChunk(checkpointTag, -1);
        }

        private void BeginLoadPrerecordedEventsChunk(CheckpointTag checkpointTag, int fromEventNumber)
        {
            _loadingPrerecordedEventsFrom = checkpointTag;
            _readDispatcher.Publish(
                new ClientMessage.ReadStreamEventsBackward(
                    Guid.NewGuid(), _readDispatcher.Envelope, _namingBuilder.GetOrderStreamName(), fromEventNumber, 100,
                    false, null), completed =>
                        {
                            switch (completed.Result)
                            {
                                case ReadStreamResult.NoStream:
                                    PrerecordedEventsLoaded(checkpointTag);
                                    break;
                                case ReadStreamResult.Success:
                                    foreach (var @event in completed.Events)
                                    {
                                        var tag = @event.Event.Metadata.ParseJson<CheckpointTag>();
                                        if (tag <= checkpointTag)
                                        {
                                            SetOrderStreamReadCompleted();
                                            break;
                                        }
                                        EnqueuePrerecordedEvent(@event.Event, tag);
                                    }
                                    if (completed.IsEndOfStream)
                                        SetOrderStreamReadCompleted();
                                    else
                                        BeginLoadPrerecordedEventsChunk(checkpointTag, completed.NextEventNumber);
                                    break;
                                default:
                                    throw new Exception("Cannot read order stream");
                            }
                        });
        }

        private void EnqueuePrerecordedEvent(EventRecord @event, CheckpointTag tag)
        {
            if (@event == null) throw new ArgumentNullException("event");
            if (tag == null) throw new ArgumentNullException("tag");
            if (@event.EventType != "$>")
                throw new ArgumentException("linkto ($>) event expected", "event");

            _loadingItemsCount++;

            var item = new Item(tag);
            _loadQueue.Push(item);

            var linkTo = Encoding.UTF8.GetString(@event.Data);
            string[] parts = linkTo.Split('@');
            int eventNumber = int.Parse(parts[0]);
            string streamId = parts[1];


            _readDispatcher.Publish(
                new ClientMessage.ReadStreamEventsBackward(
                    Guid.NewGuid(), _readDispatcher.Envelope, streamId, eventNumber, 1, true, null), completed =>
                        {
                            switch (completed.Result)
                            {
                                case ReadStreamResult.Success:
                                    if (completed.Events.Length != 1)
                                        throw new Exception(
                                            string.Format("Cannot read {0}. Error: {1}", linkTo, completed.Message));
                                    item.SetLoadedEvent(completed.Events[0]);
                                    _loadingItemsCount--;
                                    CheckAllEventsLoaded();
                                    break;
                                default:
                                    throw new Exception(
                                        string.Format("Cannot read {0}. Error: {1}", linkTo, completed.Message));
                            }
                        });
        }

        private void CheckAllEventsLoaded()
        {
            CheckpointTag lastTag = null;
            if (_orderStreamReadingCompleted && _loadingItemsCount == 0)
            {
                var number = 0;
                while (_loadQueue.Count > 0)
                {
                    var item = _loadQueue.Pop();
                    var @event = item._result;
                    lastTag = item.Tag;
                    SendPrerecordedEvent(@event, lastTag, number);
                    number++;
                }

                _loadingItemsCount = -1; // completed - do not dispatch one more time
                PrerecordedEventsLoaded(lastTag ?? _loadingPrerecordedEventsFrom);
            }
        }

        private void SetOrderStreamReadCompleted()
        {
            _orderStreamReadingCompleted = true;
            CheckAllEventsLoaded();
        }

        private class Item
        {
            internal EventStore.Core.Data.ResolvedEvent _result;
            private readonly CheckpointTag _tag;

            public Item(CheckpointTag tag)
            {
                _tag = tag;
            }

            public CheckpointTag Tag
            {
                get { return _tag; }
            }

            public void SetLoadedEvent(EventStore.Core.Data.ResolvedEvent eventLinkPair)
            {
                _result = eventLinkPair;
            }
        }
    }
}
