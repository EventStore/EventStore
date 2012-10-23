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
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Exceptions;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Checkpoint;

namespace EventStore.Core.Services.Storage
{
    public class StorageReader : IDisposable,
                                 IHandle<Message>,
                                 IHandle<SystemMessage.SystemInit>,
                                 IHandle<SystemMessage.BecomeShuttingDown>,
                                 IHandle<ClientMessage.ReadEvent>,
                                 IHandle<ClientMessage.ReadStreamEventsBackward>,
                                 IHandle<ClientMessage.ReadStreamEventsForward>,
                                 IHandle<ClientMessage.ReadAllEventsForward>,
                                 IHandle<ClientMessage.ReadAllEventsBackward>,
                                 IHandle<ClientMessage.ListStreams>,
                                 IHandle<MonitoringMessage.InternalStatsRequest>
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<StorageReader>();

        private readonly IPublisher _bus;
        private readonly IReadIndex _readIndex;
        private readonly int _threadCount;
        private readonly ICheckpoint _writerCheckpoint;
        private QueuedHandler[] _storageReaderQueues;
        private int _nextQueueNumber;

        public StorageReader(IPublisher bus, ISubscriber subscriber, IReadIndex readIndex, int threadCount, ICheckpoint writerCheckpoint)
        {
            Ensure.NotNull(bus, "bus");
            Ensure.NotNull(subscriber, "subscriber");
            Ensure.NotNull(readIndex, "readIndex");
            Ensure.Positive(threadCount, "threadCount");
            Ensure.NotNull(writerCheckpoint, "writerCheckpoint");

            _bus = bus;
            _readIndex = readIndex;
            _threadCount = threadCount;
            _writerCheckpoint = writerCheckpoint;

            SetupMessaging(subscriber);
        }

        private void SetupMessaging(ISubscriber subscriber)
        {
            var storageReaderBus = new InMemoryBus("StorageReaderBus");
            storageReaderBus.Subscribe<SystemMessage.SystemInit>(this);
            storageReaderBus.Subscribe<SystemMessage.BecomeShuttingDown>(this);
            storageReaderBus.Subscribe<ClientMessage.ReadEvent>(this);
            storageReaderBus.Subscribe<ClientMessage.ReadStreamEventsBackward>(this);
            storageReaderBus.Subscribe<ClientMessage.ReadStreamEventsForward>(this);
            storageReaderBus.Subscribe<ClientMessage.ReadAllEventsForward>(this);
            storageReaderBus.Subscribe<ClientMessage.ReadAllEventsBackward>(this);
            storageReaderBus.Subscribe<ClientMessage.ListStreams>(this);

            subscriber.Subscribe(this.WidenFrom<SystemMessage.SystemInit, Message>());
            subscriber.Subscribe(this.WidenFrom<SystemMessage.BecomeShuttingDown, Message>());
            subscriber.Subscribe(this.WidenFrom<ClientMessage.ReadEvent, Message>());
            subscriber.Subscribe(this.WidenFrom<ClientMessage.ReadStreamEventsBackward, Message>());
            subscriber.Subscribe(this.WidenFrom<ClientMessage.ReadStreamEventsForward, Message>());
            subscriber.Subscribe(this.WidenFrom<ClientMessage.ReadAllEventsForward, Message>());
            subscriber.Subscribe(this.WidenFrom<ClientMessage.ReadAllEventsBackward, Message>());
            subscriber.Subscribe(this.WidenFrom<ClientMessage.ListStreams, Message>());

            _storageReaderQueues = new QueuedHandler[_threadCount];
            for (int i = 0; i < _threadCount; ++i)
            {
                var queue = new QueuedHandler(storageReaderBus, string.Format("StorageReaderQueue #{0}", i));
                _storageReaderQueues[i] = queue;
                queue.Start();
            }
        }

        public void Handle(Message message)
        {
            var queueNumber = ((uint)Interlocked.Increment(ref _nextQueueNumber)) % _threadCount;
            _storageReaderQueues[queueNumber].Handle(message);

            // TODO AN manage this cyclic thread stopping dependency
            if (message is SystemMessage.BecomeShuttingDown)
            {
                for (int i = 0; i < _storageReaderQueues.Length; ++i)
                {
                    try
                    {
                        _storageReaderQueues[i].Stop();
                    }
                    catch (Exception exc)
                    {
                        Log.ErrorException(exc, 
                                           string.Format("Error during stopping reader QueuedHandler '{0}'.", 
                                                         _storageReaderQueues[i].Name));
                    }   
                }
                _bus.Publish(new SystemMessage.ServiceShutdown("StorageReader"));
            }
        }

        void IHandle<SystemMessage.SystemInit>.Handle(SystemMessage.SystemInit message)
        {
            _bus.Publish(new SystemMessage.StorageReaderInitializationDone());
        }

        void IHandle<SystemMessage.BecomeShuttingDown>.Handle(SystemMessage.BecomeShuttingDown message)
        {
            Dispose();
        }

        public void Dispose()
        {
            // TODO AN manage this cyclic thread stopping dependency
            //_storageReaderQueue.Stop();
        }

        void IHandle<ClientMessage.ReadEvent>.Handle(ClientMessage.ReadEvent message)
        {
            EventRecord record;
            var result = _readIndex.ReadEvent(message.EventStreamId, message.EventNumber, out record);

            if (result == SingleReadResult.Success && message.ResolveLinkTos && record != null)
                record = _readIndex.ResolveLinkToEvent(record) ?? record;

            //TODO: consider returning redirected stream and ChunkNumber number
            message.Envelope.ReplyWith(new ClientMessage.ReadEventCompleted(message.CorrelationId,
                                                                            message.EventStreamId,
                                                                            message.EventNumber,
                                                                            result,
                                                                            record));
        }

        void IHandle<ClientMessage.ReadStreamEventsForward>.Handle(ClientMessage.ReadStreamEventsForward message)
        {
            var lastCommitPosition = _readIndex.LastCommitPosition;

            EventRecord[] records;
            var result = _readIndex.ReadStreamEventsForward(message.EventStreamId, message.FromEventNumber, message.MaxCount, out records);
            var nextEventNumber = result == RangeReadResult.Success && records.Length > 0
                                          ? records[records.Length - 1].EventNumber + 1
                                          : -1;
            if (result == RangeReadResult.Success && records.Length > 1)
            {
                for (var index = 1; index < records.Length; index++)
                {
                    if (records[index].EventNumber != records[index - 1].EventNumber + 1)
                    {
                        throw new Exception(string.Format(
                                "Invalid order of events has been detected in read index for the event stream '{0}'. "
                                + "The event {1} at position {2} goes after the event {3} at position {4}",
                                message.EventStreamId,
                                records[index].EventNumber,
                                records[index].LogPosition,
                                records[index - 1].EventNumber,
                                records[index - 1].LogPosition));
                    }
                }
            }

            EventLinkPair[] resolvedPairs;
            if (result == RangeReadResult.Success && message.ResolveLinks)
            {
                resolvedPairs = ResolveLinkToEvents(records);
            }
            else
            {
                resolvedPairs = new EventLinkPair[records.Length];
                for (int i = 0; i < records.Length; ++i)
                {
                    resolvedPairs[i] = new EventLinkPair(records[i], null);
                }
            }

            int lastEventNumber = -1;
            if (message.ReturnLastEventNumber)
            {
               lastEventNumber = _readIndex.GetLastStreamEventNumber(message.EventStreamId);
            }

            message.Envelope.ReplyWith(
                    new ClientMessage.ReadStreamEventsForwardCompleted(message.CorrelationId,
                                                                       message.EventStreamId,
                                                                       resolvedPairs,
                                                                       result,
                                                                       nextEventNumber,
                                                                       records.Length == 0 ? lastCommitPosition : (long?) null,
                                                                       lastEventNumber));
        }

        void IHandle<ClientMessage.ReadStreamEventsBackward>.Handle(ClientMessage.ReadStreamEventsBackward message)
        {
            var lastCommitPosition = _readIndex.LastCommitPosition;

            EventRecord[] records;
            var result = _readIndex.ReadStreamEventsBackward(message.EventStreamId, message.FromEventNumber, message.MaxCount, out records);
            var nextEventNumber = result == RangeReadResult.Success & records.Length > 0
                                      ? records[records.Length - 1].EventNumber - 1
                                      : -1;
            if (result == RangeReadResult.Success && records.Length > 1)
            {
                for (var index = 1; index < records.Length; index++)
                {
                    if (records[index].EventNumber != records[index - 1].EventNumber - 1)
                    {
                        throw new Exception(string.Format(
                                "Invalid order of events has been detected in read index for the event stream '{0}'. "
                                + "The event {1} at position {2} goes after the event {3} at position {4}",
                                message.EventStreamId,
                                records[index].EventNumber,
                                records[index].LogPosition,
                                records[index - 1].EventNumber,
                                records[index - 1].LogPosition));
                    }
                }
            }
            EventLinkPair[] resolvedPairs;
            if (result == RangeReadResult.Success && message.ResolveLinks)
            {
                resolvedPairs = ResolveLinkToEvents(records);
            }
            else
            {
                resolvedPairs = new EventLinkPair[records.Length];
                for (int i = 0; i < records.Length; ++i)
                {
                    resolvedPairs[i] = new EventLinkPair(records[i], null);
                }
            }

            message.Envelope.ReplyWith(
                new ClientMessage.ReadStreamEventsBackwardCompleted(message.CorrelationId,
                                                                    message.EventStreamId,
                                                                    resolvedPairs,
                                                                    result,
                                                                    nextEventNumber,
                                                                    records.Length == 0 ? lastCommitPosition : (long?) null));
        }

        private EventLinkPair[] ResolveLinkToEvents(EventRecord[] records)
        {
            var resolved = new EventLinkPair[records.Length];
            for (int i = 0; i < records.Length; i++)
            {
                EventRecord eventRecord = records[i];
                EventRecord resolvedRecord = _readIndex.ResolveLinkToEvent(eventRecord);
                resolved[i] = resolvedRecord != null
                                      ? new EventLinkPair(resolvedRecord, eventRecord)
                                      : new EventLinkPair(eventRecord, null);
            }
            return resolved;
        }

        void IHandle<ClientMessage.ReadAllEventsForward>.Handle(ClientMessage.ReadAllEventsForward message)
        {
            var pos = new TFPos(message.CommitPosition, message.PreparePosition);
            var result = _readIndex.ReadAllEventsForward(pos, message.MaxCount, message.ResolveLinks);
            message.Envelope.ReplyWith(new ClientMessage.ReadAllEventsForwardCompleted(message.CorrelationId, result));
        }

        void IHandle<ClientMessage.ReadAllEventsBackward>.Handle(ClientMessage.ReadAllEventsBackward message)
        {
            var pos = new TFPos(message.CommitPosition, message.PreparePosition);
            if (pos == new TFPos(-1, -1))
            {
                var checkpoint = _writerCheckpoint.Read();
                pos = new TFPos(checkpoint, checkpoint);
            }
            var result = _readIndex.ReadAllEventsBackward(pos, message.MaxCount, message.ResolveLinks);
            message.Envelope.ReplyWith(new ClientMessage.ReadAllEventsBackwardCompleted(message.CorrelationId, result));
        }

        void IHandle<ClientMessage.ListStreams>.Handle(ClientMessage.ListStreams message)
        {
            try
            {
                EventRecord[] eventsBatch;

                // from 1 to skip $stream-created event in $streams stream
                var result = _readIndex.ReadStreamEventsForward(SystemStreams.StreamsStream, 1, int.MaxValue, out eventsBatch);
                if (result != RangeReadResult.Success)
                    throw new SystemStreamNotFoundException(
                        string.Format("Couldn't find system stream {0}, which should've been created with projection 'Index By Streams'",
                                        SystemStreams.StreamsStream));

                var streamIds = eventsBatch
                    .Select(e =>
                    {
                        var dataStr = Encoding.UTF8.GetString(e.Data);
                        var parts = dataStr.Split('@');
                        if (parts.Length < 2)
                            throw new FormatException(string.Format("{0} stream event data is in bad format: {1}. Expected: eventNumber@streamid", SystemStreams.StreamsStream, dataStr));
                        var streamid = parts[1];
                        return streamid;
                    })
                    .ToArray();

                message.Envelope.ReplyWith(new ClientMessage.ListStreamsCompleted(true, streamIds));
            }
            catch (Exception ex)
            {
                Log.ErrorException(ex, "Error while reading stream ids");
                message.Envelope.ReplyWith(new ClientMessage.ListStreamsCompleted(false, null));
            }
        }

        public void Handle(MonitoringMessage.InternalStatsRequest message)
        {
            var indexStats = _readIndex.GetStatistics();

            var stats = new Dictionary<string, object>
                {
                    {"es-readIndex-failedReadCount", indexStats.FailedReadCount},
                    {"es-readIndex-succReadCount", indexStats.SuccReadCount}
                };

            message.Envelope.ReplyWith(new MonitoringMessage.InternalStatsRequestResponse(stats));
        }
    }
}
