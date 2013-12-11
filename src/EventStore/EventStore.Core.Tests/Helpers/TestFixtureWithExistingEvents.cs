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
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Helpers
{
    public abstract class TestFixtureWithExistingEvents : TestFixtureWithReadWriteDispatchers,
                                                           IHandle<ClientMessage.ReadStreamEventsBackward>,
                                                           IHandle<ClientMessage.ReadStreamEventsForward>,
                                                           IHandle<ClientMessage.ReadAllEventsForward>,
                                                           IHandle<ClientMessage.WriteEvents>,
                                                           IHandle<ClientMessage.TransactionStart>,
                                                           IHandle<ClientMessage.TransactionWrite>,
                                                           IHandle<ClientMessage.TransactionCommit>,
                                                           IHandle<ClientMessage.DeleteStream>
    {
        public class Transaction
        {
            private long _position;
            private ClientMessage.TransactionStart _startMessage;

            private List<Tuple<int, Event >> _events =
                new List<Tuple<int, Event >>();

            public Transaction(long position, ClientMessage.TransactionStart startMessage)
            {
                _position = position;
                _startMessage = startMessage;
            }

            public void Write(ClientMessage.TransactionWrite message, ref int fakePosition)
            {
                foreach (var @event in message.Events)
                {
                    _events.Add(Tuple.Create(fakePosition, @event));
                    fakePosition += 50;
                }
            }

            public void Commit(ClientMessage.TransactionCommit message, TestFixtureWithExistingEvents fixture)
            {
                var commitPosition = fixture._fakePosition;
                fixture._fakePosition += 50;
                fixture.ProcessWrite(
                    message.Envelope, message.CorrelationId, _startMessage.EventStreamId, _startMessage.ExpectedVersion,
                    _events.Select(v => v.Item2).ToArray(),
                    (f, l) =>
                    new ClientMessage.TransactionCommitCompleted(
                        message.CorrelationId, message.TransactionId, f, l),
                    new ClientMessage.TransactionCommitCompleted(
                        message.CorrelationId, message.TransactionId, OperationResult.WrongExpectedVersion, "Wrong expected version"),
                    _events.Select(v => (long)v.Item1).ToArray(), commitPosition);
            }
        }

        protected TestHandler<ClientMessage.ReadStreamEventsBackward> _listEventsHandler;

        protected readonly Dictionary<string, List<EventRecord>> _lastMessageReplies =
            new Dictionary<string, List<EventRecord>>();

        protected readonly SortedList<TFPos, EventRecord> _all = new SortedList<TFPos, EventRecord>();

        protected readonly HashSet<string> _deletedStreams = new HashSet<string>();

        protected readonly Dictionary<long, Transaction> _activeTransactions = new Dictionary<long, Transaction>();

        private int _fakePosition = 100;
        private bool _allWritesSucceed;
        private readonly HashSet<string> _writesToSucceed = new HashSet<string>();
        private bool _allWritesQueueUp;
        private Queue<ClientMessage.WriteEvents> _writesQueue;
        private bool _readAllEnabled;
        private bool _noOtherStreams;

        protected TFPos ExistingEvent(string streamId, string eventType, string eventMetadata, string eventData, bool isJson = false)
        {
            List<EventRecord> list;
            if (!_lastMessageReplies.TryGetValue(streamId, out list) || list == null)
            {
                list = new List<EventRecord>();
                _lastMessageReplies[streamId] = list;
            }
            var eventRecord = new EventRecord(
                list.Count,
                new PrepareLogRecord(
                    _fakePosition, Guid.NewGuid(), Guid.NewGuid(), _fakePosition, 0, streamId, list.Count - 1,
                    _timeProvider.Now,
                    PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd | (isJson ? PrepareFlags.IsJson : 0),
                    eventType, Helper.UTF8NoBom.GetBytes(eventData),
                    eventMetadata == null ? new byte[0] : Helper.UTF8NoBom.GetBytes(eventMetadata)));
            list.Add(eventRecord);
            var eventPosition = new TFPos(_fakePosition + 50, _fakePosition);
            _all.Add(eventPosition, eventRecord);
            _fakePosition += 100;
            return eventPosition;
        }

        protected void EnableReadAll()
        {
            _readAllEnabled = true;
        }

        protected void NoStream(string streamId)
        {
            _lastMessageReplies[streamId] = null;
        }

        protected void NoOtherStreams()
        {
            _noOtherStreams = true;
        }

        protected void DeletedStream(string streamId)
        {
            _deletedStreams.Add(streamId);
        }

        protected void AllWritesSucceed()
        {
            _allWritesSucceed = true;
        }

        protected void AllWritesToSucceed(string streamId)
        {
            _writesToSucceed.Add(streamId);
        }

        protected void AllWritesQueueUp()
        {
            _allWritesQueueUp = true;
        }

        protected void OneWriteCompletes()
        {
            var message = _writesQueue.Dequeue();
            ProcessWrite(
                message.Envelope, message.CorrelationId, message.EventStreamId, message.ExpectedVersion, message.Events,
                (firstEventNumber, lastEventNumber) => new ClientMessage.WriteEventsCompleted(message.CorrelationId, firstEventNumber, lastEventNumber),
                new ClientMessage.WriteEventsCompleted(
                    message.CorrelationId, OperationResult.WrongExpectedVersion, "wrong expected version"));
        }

        protected void AllWriteComplete()
        {
            while (_writesQueue.Count > 0)
                OneWriteCompletes();
        }

        [SetUp]
        public void setup1()
        {
            _writesQueue = new Queue<ClientMessage.WriteEvents>();
            _listEventsHandler = new TestHandler<ClientMessage.ReadStreamEventsBackward>();
            _bus.Subscribe(_listEventsHandler);
            _bus.Subscribe<ClientMessage.WriteEvents>(this);
            _bus.Subscribe<ClientMessage.ReadStreamEventsBackward>(this);
            _bus.Subscribe<ClientMessage.ReadStreamEventsForward>(this);
            _bus.Subscribe<ClientMessage.ReadAllEventsForward>(this);
            _bus.Subscribe<ClientMessage.DeleteStream>(this);
            _bus.Subscribe<ClientMessage.TransactionStart>(this);
            _bus.Subscribe<ClientMessage.TransactionWrite>(this);
            _bus.Subscribe<ClientMessage.TransactionCommit>(this);
            _bus.Subscribe(_readDispatcher);
            _bus.Subscribe(_writeDispatcher);
            _bus.Subscribe(_ioDispatcher.StreamDeleter);
            _lastMessageReplies.Clear();
            _deletedStreams.Clear();
            _all.Clear();
            _readAllEnabled = false;
            _fakePosition = 100;
            _activeTransactions.Clear();
            Given1();
            Given();
        }

        protected virtual void Given1()
        {
        }

        protected virtual void Given()
        {
        }

        void IHandle<ClientMessage.ReadStreamEventsBackward>.Handle(ClientMessage.ReadStreamEventsBackward message)
        {
            List<EventRecord> list;
            if (_deletedStreams.Contains(message.EventStreamId))
            {
                message.Envelope.ReplyWith(
                    new ClientMessage.ReadStreamEventsBackwardCompleted(
                        message.CorrelationId, message.EventStreamId, message.FromEventNumber, message.MaxCount,
                        ReadStreamResult.StreamDeleted, new ResolvedEvent[0], null, false, string.Empty, -1, EventNumber.DeletedStream, true, _fakePosition));
                            
            }
            else if (_lastMessageReplies.TryGetValue(message.EventStreamId, out list) || _noOtherStreams)
            {
                if (list != null && list.Count > 0 && (list.Last().EventNumber >= message.FromEventNumber)
                    || (message.FromEventNumber == -1))
                {
                    ResolvedEvent[] records =
                        list.Safe()
                            .Reverse()
                            .SkipWhile(v => message.FromEventNumber != -1 && v.EventNumber > message.FromEventNumber)
                            .Take(message.MaxCount)
                            .Select(v => BuildEvent(v, message.ResolveLinkTos))
                            .ToArray();
                    message.Envelope.ReplyWith(
                        new ClientMessage.ReadStreamEventsBackwardCompleted(
                            message.CorrelationId, message.EventStreamId,
                            message.FromEventNumber == -1
                                ? (EnumerableExtensions.IsEmpty(list) ? -1 : list.Last().EventNumber)
                                : message.FromEventNumber, message.MaxCount, ReadStreamResult.Success, records, null, false,
                            string.Empty,
                            nextEventNumber: records.Length > 0 ? records.Last().Event.EventNumber - 1 : -1,
                            lastEventNumber: list.Safe().Any() ? list.Safe().Last().EventNumber : -1,
                            isEndOfStream: records.Length == 0 || records.Last().Event.EventNumber == 0,
                            tfLastCommitPosition: _fakePosition));
                }
                else
                {
                    if (list == null)
                    {
                        message.Envelope.ReplyWith(
                            new ClientMessage.ReadStreamEventsBackwardCompleted(
                                message.CorrelationId, message.EventStreamId, message.FromEventNumber, message.MaxCount,
                                ReadStreamResult.NoStream, new ResolvedEvent[0], null, false, "", nextEventNumber: -1, lastEventNumber: -1,
                                isEndOfStream: true, 
                                tfLastCommitPosition: _fakePosition));
                        return;
                    }
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


        public void Handle(ClientMessage.ReadStreamEventsForward message)
        {
            List<EventRecord> list;
            if (_deletedStreams.Contains(message.EventStreamId))
            {
                message.Envelope.ReplyWith(
                    new ClientMessage.ReadStreamEventsBackwardCompleted(
                        message.CorrelationId, message.EventStreamId, message.FromEventNumber, message.MaxCount,
                        ReadStreamResult.StreamDeleted, new ResolvedEvent[0], null, false, string.Empty, -1, EventNumber.DeletedStream, true, _fakePosition));
                            
            }
            else if (_lastMessageReplies.TryGetValue(message.EventStreamId, out list) || _noOtherStreams)
            {
                if (list != null && list.Count > 0 && message.FromEventNumber >= 0)
                {
                    ResolvedEvent[] records =
                        list.Safe()
                            .SkipWhile(v => v.EventNumber < message.FromEventNumber)
                            .Take(message.MaxCount)
                            .Select(v => BuildEvent(v, message.ResolveLinkTos))
                            .ToArray();
                    var lastEventNumber = list.Safe().Any() ? list.Safe().Last().EventNumber : -1;
                    message.Envelope.ReplyWith(
                        new ClientMessage.ReadStreamEventsForwardCompleted(
                            message.CorrelationId, message.EventStreamId,
                            message.FromEventNumber, message.MaxCount, ReadStreamResult.Success, records, null, false,
                            string.Empty,
                            nextEventNumber: records.Length > 0 ? records.Last().Event.EventNumber + 1 : lastEventNumber + 1,
                            lastEventNumber: lastEventNumber,
                            isEndOfStream: records.Length == 0 || records.Last().Event.EventNumber == list.Last().EventNumber,
                            tfLastCommitPosition: _fakePosition));
                }
                else
                {
                    if (list == null)
                    {
                        message.Envelope.ReplyWith(
                            new ClientMessage.ReadStreamEventsForwardCompleted(
                                message.CorrelationId, message.EventStreamId, message.FromEventNumber, message.MaxCount,
                                ReadStreamResult.NoStream, new ResolvedEvent[0], null, false, "", nextEventNumber: -1, lastEventNumber: -1,
                                isEndOfStream: true, 
                                tfLastCommitPosition: _fakePosition));
                        return;
                    }
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

        private ResolvedEvent BuildEvent(EventRecord x, bool resolveLinks)
        {
            if (x.EventType == "$>" && resolveLinks)
            {
                var parts = Helper.UTF8NoBom.GetString(x.Data).Split('@');
                var list = _lastMessageReplies[parts[1]];
                var eventNumber = int.Parse(parts[0]);
                var target = list[eventNumber];

                return new ResolvedEvent(target, x);
            }
            else
                return new ResolvedEvent(x, null);
        }

        private ResolvedEvent BuildEvent(EventRecord x, bool resolveLinks, long commitPosition)
        {
            if (x.EventType == "$>" && resolveLinks)
            {
                var parts = Helper.UTF8NoBom.GetString(x.Data).Split('@');
                var list = _lastMessageReplies[parts[1]];
                var eventNumber = int.Parse(parts[0]);
                var target = list[eventNumber];

                return new ResolvedEvent(target, x, commitPosition);
            }
            else
                return new ResolvedEvent(x, commitPosition);
        }

        public void Handle(ClientMessage.WriteEvents message)
        {
            if (_allWritesSucceed || _writesToSucceed.Contains(message.EventStreamId))
            {
                ProcessWrite(
                    message.Envelope, message.CorrelationId, message.EventStreamId, message.ExpectedVersion,
                    message.Events,
                    (firstEventNumber, lastEventNumber) =>
                        new ClientMessage.WriteEventsCompleted(message.CorrelationId, firstEventNumber, lastEventNumber),
                    new ClientMessage.WriteEventsCompleted(
                        message.CorrelationId, OperationResult.WrongExpectedVersion, "wrong expected version"));
            }
            else if (_allWritesQueueUp)
                _writesQueue.Enqueue(message);
        }

        private void ProcessWrite<T>(IEnvelope envelope, Guid correlationId, string streamId, int expectedVersion, Event[] events, Func<int, int, T> writeEventsCompleted, T wrongExpectedVersionResponse, long[] positions = null, int? commitPosition = null) where T : Message
        {
            if (positions == null)
            {
                positions = new long[events.Length];
                for (int i = 0; i < positions.Length; i++)
                {
                    positions[i] = _fakePosition;
                    _fakePosition += 100;
                }
            }
            List<EventRecord> list;
            if (!_lastMessageReplies.TryGetValue(streamId, out list) || list == null)
            {
                list = new List<EventRecord>();
                _lastMessageReplies[streamId] = list;
            }
            if (expectedVersion != EventStore.ClientAPI.ExpectedVersion.Any)
            {
                if (expectedVersion != list.Count - 1)
                {
                    envelope.ReplyWith(wrongExpectedVersionResponse);
                    return;
                }
            }
            var eventRecords = (from ep in events.Zip(positions, (@event, position) => new {@event, position})
                                let e = ep.@event
                                let eventNumber = list.Count
                                //NOTE: ASSUMES STAYS ENUMERABLE
                                let tfPosition = ep.position
                                select
                                    new
                                        {
                                            position = tfPosition,
                                            record =
                                    new EventRecord(
                                    eventNumber, tfPosition, correlationId, e.EventId, tfPosition, 0, streamId,
                                    ExpectedVersion.Any, _timeProvider.Now,
                                    PrepareFlags.SingleWrite | (e.IsJson ? PrepareFlags.IsJson : PrepareFlags.None),
                                    e.EventType, e.Data, e.Metadata)
                                        }); //NOTE: DO NOT MAKE ARRAY 
            foreach (var eventRecord in eventRecords)
            {
                list.Add(eventRecord.record);
                _all.Add(new TFPos(commitPosition ?? eventRecord.position + 50, eventRecord.position), eventRecord.record);
            }

            var firstEventNumber = list.Count - events.Length;
            envelope.ReplyWith(writeEventsCompleted(firstEventNumber, firstEventNumber + events.Length - 1));
        }

        public void Handle(ClientMessage.DeleteStream message)
        {
            List<EventRecord> list;
            if (_deletedStreams.Contains(message.EventStreamId))
            {
                message.Envelope.ReplyWith(new ClientMessage.DeleteStreamCompleted(message.CorrelationId, OperationResult.StreamDeleted, string.Empty));
                return;
            }
            if (!_lastMessageReplies.TryGetValue(message.EventStreamId, out list) || list == null)
            {
                message.Envelope.ReplyWith(new ClientMessage.DeleteStreamCompleted(message.CorrelationId, OperationResult.WrongExpectedVersion, string.Empty));
                return;
            }
            _deletedStreams.Add(message.EventStreamId);
                message.Envelope.ReplyWith(new ClientMessage.DeleteStreamCompleted(message.CorrelationId, OperationResult.Success, string.Empty));
        }

        public void Handle(ClientMessage.ReadAllEventsForward message)
        {
            if (!_readAllEnabled)
                return;
            var from = new TFPos(message.CommitPosition, message.PreparePosition);
            var records = _all.SkipWhile(v => v.Key < from).Take(message.MaxCount).ToArray();
            var list = new List<ResolvedEvent>();
            var pos = from;
            var next = pos;
            var prev = new TFPos(pos.CommitPosition, Int64.MaxValue);
            foreach (KeyValuePair<TFPos, EventRecord> record in records)
            {
                pos = record.Key;
                next = new TFPos(pos.CommitPosition, pos.PreparePosition + 1);
                list.Add(BuildEvent(record.Value, message.ResolveLinkTos, record.Key.CommitPosition));
            }
            var events = list.ToArray();
            message.Envelope.ReplyWith(
                new ClientMessage.ReadAllEventsForwardCompleted(
                    message.CorrelationId, ReadAllResult.Success, "", events, null, false, message.MaxCount, pos, next, prev,
                    _fakePosition));
        }

        public void Handle(ClientMessage.TransactionStart message)
        {
            var transactionId = _fakePosition;
            _activeTransactions.Add(transactionId, new Transaction(transactionId, message));
            _fakePosition += 50;
            message.Envelope.ReplyWith(
                new ClientMessage.TransactionStartCompleted(
                    message.CorrelationId, transactionId, OperationResult.Success, ""));
        }

        public void Handle(ClientMessage.TransactionWrite message)
        {
            Transaction transaction;
            if (!_activeTransactions.TryGetValue(message.TransactionId, out transaction))
            {
                message.Envelope.ReplyWith(
                    new ClientMessage.TransactionWriteCompleted(
                        message.CorrelationId, message.TransactionId, OperationResult.InvalidTransaction,
                        "Transaction not found"));
            }
            else
            {
                transaction.Write(message, ref _fakePosition);
            }
        }

        public void Handle(ClientMessage.TransactionCommit message)
        {
            Transaction transaction;
            if (!_activeTransactions.TryGetValue(message.TransactionId, out transaction))
            {
                message.Envelope.ReplyWith(
                    new ClientMessage.TransactionWriteCompleted(
                        message.CorrelationId, message.TransactionId, OperationResult.InvalidTransaction,
                        "Transaction not found"));
            }
            else
            {
                transaction.Commit(message, this);
            }
        }

        protected TFPos GetTfPos(string streamId, int eventNumber)
        {
            return _all.Last(v => v.Value.EventStreamId == streamId && v.Value.EventNumber == eventNumber).Key;
        }

        public void AssertLastEvent(string streamId, string data, string message = null)
        {
            message = message ?? string.Format("Invalid last event in the '{0}' stream. ", streamId);
            List<EventRecord> events;
            Assert.That(_lastMessageReplies.TryGetValue(streamId, out events), message + "The stream does not exist.");
            Assert.IsNotEmpty(events, message + "The stream is empty.");
            var last = events[events.Count - 1];
            Assert.AreEqual(data,Encoding.UTF8.GetString(last.Data));
        }

        public void AssertEvent(string streamId, int eventNumber, string data)
        {
            throw new NotImplementedException();
        }

        public void AssertEmptyStream(string streamId)
        {
            throw new NotImplementedException();
        }
    }
}

