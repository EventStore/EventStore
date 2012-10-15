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
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;

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
                                 IHandle<ClientMessage.ListStreams>,
                                 IHandle<MonitoringMessage.InternalStatsRequest>
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<StorageReader>();

        private readonly IPublisher _bus;
        private readonly IReadIndex _readIndex;
        private readonly int _threadCount;
        private QueuedHandler[] _storageReaderQueues;
        private int _nextQueueNumber;

        public StorageReader(IPublisher bus, ISubscriber subscriber, IReadIndex readIndex, int threadCount)
        {
            Ensure.NotNull(bus, "bus");
            Ensure.NotNull(subscriber, "subscriber");
            Ensure.NotNull(readIndex, "readIndex");
            Ensure.Positive(threadCount, "threadCount");

            _bus = bus;
            _readIndex = readIndex;
            _threadCount = threadCount;

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
            storageReaderBus.Subscribe<ClientMessage.ListStreams>(this);

            subscriber.Subscribe(this.WidenFrom<SystemMessage.SystemInit, Message>());
            subscriber.Subscribe(this.WidenFrom<SystemMessage.BecomeShuttingDown, Message>());
            subscriber.Subscribe(this.WidenFrom<ClientMessage.ReadEvent, Message>());
            subscriber.Subscribe(this.WidenFrom<ClientMessage.ReadStreamEventsBackward, Message>());
            subscriber.Subscribe(this.WidenFrom<ClientMessage.ReadStreamEventsForward, Message>());
            subscriber.Subscribe(this.WidenFrom<ClientMessage.ReadAllEventsForward, Message>());
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

        void IHandle<ClientMessage.ReadStreamEventsBackward>.Handle(ClientMessage.ReadStreamEventsBackward message)
        {
            EventRecord[] records;
            EventRecord[] links = null;

            var lastCommitPosition = _readIndex.LastCommitPosition;

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
            if (result == RangeReadResult.Success && message.ResolveLinks)
                links = ResolveLinkToEvents(records);

            message.Envelope.ReplyWith(
                new ClientMessage.ReadStreamEventsBackwardCompleted(message.CorrelationId,
                                                                    message.EventStreamId,
                                                                    records,
                                                                    links,
                                                                    result,
                                                                    nextEventNumber,
                                                                    records.Length == 0 ? lastCommitPosition : (long?) null));
        }

        void IHandle<ClientMessage.ReadStreamEventsForward>.Handle(ClientMessage.ReadStreamEventsForward message)
        {
            // TODO AN resolving should belong to ReadIndex, not to StorageReader

            EventRecord[] records;
            EventRecord[] links = null;
            
            var lastCommitPosition = _readIndex.LastCommitPosition;
            
            var result = _readIndex.ReadStreamEventsForward(message.EventStreamId, message.FromEventNumber, message.MaxCount, out records);
            var nextEventNumber = result == RangeReadResult.Success & records.Length > 0
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
            if (result == RangeReadResult.Success && message.ResolveLinks)
                links = ResolveLinkToEvents(records);

            message.Envelope.ReplyWith(
                new ClientMessage.ReadStreamEventsForwardCompleted(message.CorrelationId,
                                                                   message.EventStreamId,
                                                                   records,
                                                                   links,
                                                                   result,
                                                                   nextEventNumber,
                                                                   records.Length == 0 ? lastCommitPosition : (long?) null));
        }

        private EventRecord[] ResolveLinkToEvents(EventRecord[] records)
        {
            var links = new EventRecord[records.Length];
            for (int i = 0; i < records.Length; i++)
            {
                EventRecord eventRecord = records[i];
                EventRecord record = _readIndex.ResolveLinkToEvent(eventRecord);
                if (record != null)
                {
                    links[i] = eventRecord;
                    records[i] = record;
                }
            }
            return links;
        }

        void IHandle<ClientMessage.ReadAllEventsForward>.Handle(ClientMessage.ReadAllEventsForward message)
        {
            // TODO AN minus one hack is ugly, ask Yuriy to maybe do something with this
            var result = _readIndex.ReadAllEventsForward(new TFPos(message.CommitPosition, message.PreparePosition - 1),
                                                         message.MaxCount,
                                                         message.ResolveLinks);
            message.Envelope.ReplyWith(new ClientMessage.ReadAllEventsForwardCompleted(message.CorrelationId,
                                                                                       result.Records.ToArray(),
                                                                                       RangeReadResult.Success));
            }

        void IHandle<ClientMessage.ListStreams>.Handle(ClientMessage.ListStreams message)
        {
            try
            {
                var streams = _readIndex.GetStreamIds();
                message.Envelope.ReplyWith(new ClientMessage.ListStreamsCompleted(streams != null,streams));    
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
