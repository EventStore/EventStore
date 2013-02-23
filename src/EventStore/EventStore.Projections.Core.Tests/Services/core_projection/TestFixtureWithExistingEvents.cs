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
using System.Linq;
using System.Text;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests;
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Tests.Services.projections_manager.managed_projection;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    [MightyMooseIgnore]
    public abstract class TestFixtureWithExistingEvents : TestFixtureWithReadWriteDisaptchers,
                                                          IHandle<ClientMessage.ReadStreamEventsBackward>,
                                                          IHandle<ClientMessage.WriteEvents>,
                                                          IHandle<ProjectionCoreServiceMessage.Tick>
    {
        protected TestHandler<ClientMessage.ReadStreamEventsBackward> _listEventsHandler;

        protected readonly Dictionary<string, List<EventRecord>> _lastMessageReplies = new Dictionary<string, List<EventRecord>>();

        private int _fakePosition = 100;
        private bool _allWritesSucceed;
        private HashSet<string> _writesToSucceed = new HashSet<string>();
        private bool _allWritesQueueUp;
        private Queue<ClientMessage.WriteEvents> _writesQueue;
        private long _lastPosition;
        private bool _ticksAreHandledImmediately;

        protected void ExistingEvent(string streamId, string eventType, string eventMetadata, string eventData)
        {
            List<EventRecord> list;
            if (!_lastMessageReplies.TryGetValue(streamId, out list) || list == null)
            {
                list = new List<EventRecord>();
                _lastMessageReplies[streamId] = list;
            }
            list.Add(
                new EventRecord(
                    list.Count,
                    new PrepareLogRecord(
                        _fakePosition, Guid.NewGuid(), Guid.NewGuid(), _fakePosition, 0, streamId, list.Count - 1,
                        DateTime.UtcNow, PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd, eventType,
                        Encoding.UTF8.GetBytes(eventData),
                        eventMetadata == null ? new byte[0] : Encoding.UTF8.GetBytes(eventMetadata))));
            _fakePosition += 100;
        }

        protected void NoStream(string streamId)
        {
            _lastMessageReplies[streamId] = null;
        }

        protected void AllWritesSucceed()
        {
            _allWritesSucceed = true;
        }

        protected void AllWritesToSucceed(string streamId)
        {
            _writesToSucceed.Add(streamId);
        }

        protected void TicksAreHandledImmediately()
        {
            _ticksAreHandledImmediately = true;
        }

        protected void AllWritesQueueUp()
        {
            _allWritesQueueUp = true;
        }

        protected void OneWriteCompletes()
        {
            var message = _writesQueue.Dequeue();
            ProcessWrite(message);
        }

        protected void AllWriteComplete()
        {
            while (_writesQueue.Count > 0)
                OneWriteCompletes();
        }

        [SetUp]
        public void setup1()
        {
            _ticksAreHandledImmediately = false;
            _writesQueue = new Queue<ClientMessage.WriteEvents>();
            _listEventsHandler = new TestHandler<ClientMessage.ReadStreamEventsBackward>();
            _readDispatcher = new RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>(
                _bus, v => v.CorrelationId, v => v.CorrelationId, new PublishEnvelope(_bus));
            _writeDispatcher = new RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted>(
                _bus, v => v.CorrelationId, v => v.CorrelationId, new PublishEnvelope(_bus));
            _bus.Subscribe(_listEventsHandler);
            _bus.Subscribe<ClientMessage.WriteEvents>(this);
            _bus.Subscribe<ClientMessage.ReadStreamEventsBackward>(this);
            _bus.Subscribe<ProjectionCoreServiceMessage.Tick>(this);
            _bus.Subscribe(_readDispatcher);
            _bus.Subscribe(_writeDispatcher);
            _lastMessageReplies.Clear();
            Given();
            _lastPosition =
                _lastMessageReplies.Values.Max(v => v == null ? (long?) 0 : v.Max(u => (long?) u.LogPosition))
                ?? 0 + 100;
        }

        protected virtual void Given()
        {
        }

        void IHandle<ClientMessage.ReadStreamEventsBackward>.Handle(ClientMessage.ReadStreamEventsBackward message)
        {
            List<EventRecord> list;
            if (_lastMessageReplies.TryGetValue(message.EventStreamId, out list))
            {
                if (list != null && list.Count > 0 && (list.Last().EventNumber >= message.FromEventNumber)
                    || (message.FromEventNumber == -1))
                {
                    ResolvedEvent[] records = list.Safe()
                                                  .Reverse()
                                                  .SkipWhile(v => message.FromEventNumber != -1 && v.EventNumber > message.FromEventNumber)
                                                  .Take(message.MaxCount)
                                                  .Select(x => new ResolvedEvent(x, null))
                                                  .ToArray();
                    message.Envelope.ReplyWith(
                            new ClientMessage.ReadStreamEventsBackwardCompleted(
                                    message.CorrelationId,
                                    message.EventStreamId,
                                    message.FromEventNumber == -1 ? (list.IsEmpty() ? -1 : list.Last().EventNumber) : message.FromEventNumber,
                                    message.MaxCount,
                                    ReadStreamResult.Success,
                                    records,
                                    string.Empty,
                                    nextEventNumber: records.Length > 0 ? records.Last().Event.EventNumber - 1 : -1,
                                    lastEventNumber: list.Safe().Any() ? list.Safe().Last().EventNumber : -1,
                                    isEndOfStream: records.Length > 0 && records.Last().Event.EventNumber == 0,
                                    lastCommitPosition: _lastPosition));
                }
                else
                {
                    throw new NotImplementedException();
/*
                    message.Envelope.ReplyWith(
                            new ClientMessage.ReadStreamEventsBackwardCompleted(
                                    message.CorrelationId,
                                    message.EventStreamId,
                                    new EventLinkPair[0],
                                    ReadStreamResult.Success,
                                    nextEventNumber: -1,
                                    lastEventNumber: list.Safe().Last().EventNumber,
                                    isEndOfStream: true,// NOTE AN: don't know how to correctly determine this here
                                    lastCommitPosition: _lastPosition));
*/
                }
            }
        }

        public void Handle(ClientMessage.WriteEvents message)
        {
            if (_allWritesSucceed || _writesToSucceed.Contains(message.EventStreamId))
            {
                ProcessWrite(message);
            }
            else if (_allWritesQueueUp)
                _writesQueue.Enqueue(message);
        }

        private void ProcessWrite(ClientMessage.WriteEvents message)
        {
            List<EventRecord> list;
            if (!_lastMessageReplies.TryGetValue(message.EventStreamId, out list) || list == null)
            {
                list = new List<EventRecord>();
                _lastMessageReplies[message.EventStreamId] = list;
            }
            foreach (var eventRecord in from e in message.Events
                                        select
                                            new EventRecord(
                                            list.Count, list.Count*1000, message.CorrelationId, e.EventId, list.Count*1000, 0,
                                            message.EventStreamId, ExpectedVersion.Any, DateTime.UtcNow,
                                            PrepareFlags.SingleWrite, e.EventType, e.Data, e.Metadata))
            {
                list.Add(eventRecord);
            }

            message.Envelope.ReplyWith(new ClientMessage.WriteEventsCompleted(message.CorrelationId, list.Count - message.Events.Length));
        }

        public void Handle(ProjectionCoreServiceMessage.Tick message)
        {
            if (_ticksAreHandledImmediately)
                message.Action();
        }
    }
}
