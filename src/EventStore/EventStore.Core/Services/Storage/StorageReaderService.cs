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
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Checkpoint;

namespace EventStore.Core.Services.Storage
{
    public class StorageReaderService : IHandle<SystemMessage.SystemInit>,
                                        IHandle<SystemMessage.BecomeShuttingDown>,
                                        IHandle<SystemMessage.BecomeShutdown>,
                                        IHandle<MonitoringMessage.InternalStatsRequest>
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<StorageReaderService>();

        private readonly IPublisher _bus;
        private readonly IReadIndex _readIndex;
        private readonly int _threadCount;

        private readonly MultiQueuedHandler _workersMultiHandler;

        public StorageReaderService(IPublisher bus, ISubscriber subscriber, IReadIndex readIndex, int threadCount, ICheckpoint writerCheckpoint)
        {
            Ensure.NotNull(bus, "bus");
            Ensure.NotNull(subscriber, "subscriber");
            Ensure.NotNull(readIndex, "readIndex");
            Ensure.Positive(threadCount, "threadCount");
            Ensure.NotNull(writerCheckpoint, "writerCheckpoint");

            _bus = bus;
            _readIndex = readIndex;
            _threadCount = threadCount;

            var readerWorker = new StorageReaderWorker(readIndex, writerCheckpoint);
            var storageReaderBus = new InMemoryBus("StorageReaderBus", watchSlowMsg: false);
            storageReaderBus.Subscribe<ClientMessage.ReadEvent>(readerWorker);
            storageReaderBus.Subscribe<ClientMessage.ReadStreamEventsBackward>(readerWorker);
            storageReaderBus.Subscribe<ClientMessage.ReadStreamEventsForward>(readerWorker);
            storageReaderBus.Subscribe<ClientMessage.ReadAllEventsForward>(readerWorker);
            storageReaderBus.Subscribe<ClientMessage.ReadAllEventsBackward>(readerWorker);
            storageReaderBus.Subscribe<StorageMessage.CheckStreamAccess>(readerWorker);

            _workersMultiHandler = new MultiQueuedHandler(
                _threadCount,
                queueNum => new QueuedHandler(storageReaderBus, 
                                              string.Format("StorageReaderQueue #{0}", queueNum + 1),
                                              groupName: "StorageReaderQueue",
                                              watchSlowMsg: true,
                                              slowMsgThreshold: TimeSpan.FromMilliseconds(200)));
            _workersMultiHandler.Start();

            subscriber.Subscribe(_workersMultiHandler.WidenFrom<ClientMessage.ReadEvent, Message>());
            subscriber.Subscribe(_workersMultiHandler.WidenFrom<ClientMessage.ReadStreamEventsBackward, Message>());
            subscriber.Subscribe(_workersMultiHandler.WidenFrom<ClientMessage.ReadStreamEventsForward, Message>());
            subscriber.Subscribe(_workersMultiHandler.WidenFrom<ClientMessage.ReadAllEventsForward, Message>());
            subscriber.Subscribe(_workersMultiHandler.WidenFrom<ClientMessage.ReadAllEventsBackward, Message>());
            subscriber.Subscribe(_workersMultiHandler.WidenFrom<StorageMessage.CheckStreamAccess, Message>());
        }

        void IHandle<SystemMessage.SystemInit>.Handle(SystemMessage.SystemInit message)
        {
            _bus.Publish(new SystemMessage.StorageReaderInitializationDone());
        }

        void IHandle<SystemMessage.BecomeShuttingDown>.Handle(SystemMessage.BecomeShuttingDown message)
        {
            try
            {
                _workersMultiHandler.Stop();
            }
            catch (Exception exc)
            {
                Log.ErrorException(exc, "Error during stopping readers multi handler.");
            }

            _bus.Publish(new SystemMessage.ServiceShutdown("StorageReader"));
        }

        void IHandle<SystemMessage.BecomeShutdown>.Handle(SystemMessage.BecomeShutdown message)
        {
            // by now (in case of successful shutdown process, all readers and writers should not be using ReadIndex
            _readIndex.Close();
        }

        void IHandle<MonitoringMessage.InternalStatsRequest>.Handle(MonitoringMessage.InternalStatsRequest message)
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
