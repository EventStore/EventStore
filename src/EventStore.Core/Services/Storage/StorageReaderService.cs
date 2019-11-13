using System;
using System.Collections.Generic;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Checkpoint;

namespace EventStore.Core.Services.Storage {
	public class StorageReaderService : IHandle<SystemMessage.SystemInit>,
		IHandle<SystemMessage.BecomeShuttingDown>,
		IHandle<SystemMessage.BecomeShutdown>,
		IHandle<MonitoringMessage.InternalStatsRequest> {
		private static readonly ILogger Log = LogManager.GetLoggerFor<StorageReaderService>();

		private readonly IPublisher _bus;
		private readonly IReadIndex _readIndex;
		private readonly int _threadCount;

		private readonly MultiQueuedHandler _workersMultiHandler;

		public StorageReaderService(IPublisher bus, ISubscriber subscriber, IReadIndex readIndex, int threadCount,
			ICheckpoint writerCheckpoint) {
			Ensure.NotNull(bus, "bus");
			Ensure.NotNull(subscriber, "subscriber");
			Ensure.NotNull(readIndex, "readIndex");
			Ensure.Positive(threadCount, "threadCount");
			Ensure.NotNull(writerCheckpoint, "writerCheckpoint");

			_bus = bus;
			_readIndex = readIndex;
			_threadCount = threadCount;

			StorageReaderWorker[] readerWorkers = new StorageReaderWorker[threadCount];
			InMemoryBus[] storageReaderBuses = new InMemoryBus[threadCount];
			for (var i = 0; i < threadCount; i++) {
				readerWorkers[i] = new StorageReaderWorker(bus, readIndex, writerCheckpoint, i);
				storageReaderBuses[i] = new InMemoryBus("StorageReaderBus", watchSlowMsg: false);
				storageReaderBuses[i].Subscribe<ClientMessage.ReadEvent>(readerWorkers[i]);
				storageReaderBuses[i].Subscribe<ClientMessage.ReadStreamEventsBackward>(readerWorkers[i]);
				storageReaderBuses[i].Subscribe<ClientMessage.ReadStreamEventsForward>(readerWorkers[i]);
				storageReaderBuses[i].Subscribe<ClientMessage.ReadAllEventsForward>(readerWorkers[i]);
				storageReaderBuses[i].Subscribe<ClientMessage.ReadAllEventsBackward>(readerWorkers[i]);
				storageReaderBuses[i].Subscribe<ClientMessage.FilteredReadAllEventsForward>(readerWorkers[i]);
				storageReaderBuses[i].Subscribe<ClientMessage.FilteredReadAllEventsBackward>(readerWorkers[i]);
				storageReaderBuses[i].Subscribe<StorageMessage.CheckStreamAccess>(readerWorkers[i]);
				storageReaderBuses[i].Subscribe<StorageMessage.BatchLogExpiredMessages>(readerWorkers[i]);
			}

			_workersMultiHandler = new MultiQueuedHandler(
				_threadCount,
				queueNum => new QueuedHandlerThreadPool(storageReaderBuses[queueNum],
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
			subscriber.Subscribe(_workersMultiHandler.WidenFrom<ClientMessage.FilteredReadAllEventsForward, Message>());
			subscriber.Subscribe(_workersMultiHandler.WidenFrom<ClientMessage.FilteredReadAllEventsBackward, Message>());
			subscriber.Subscribe(_workersMultiHandler.WidenFrom<StorageMessage.CheckStreamAccess, Message>());
			subscriber.Subscribe(_workersMultiHandler.WidenFrom<StorageMessage.BatchLogExpiredMessages, Message>());
		}

		void IHandle<SystemMessage.SystemInit>.Handle(SystemMessage.SystemInit message) {
			_bus.Publish(new SystemMessage.ServiceInitialized("StorageReader"));
		}

		void IHandle<SystemMessage.BecomeShuttingDown>.Handle(SystemMessage.BecomeShuttingDown message) {
			try {
				_workersMultiHandler.Stop();
			} catch (Exception exc) {
				Log.ErrorException(exc, "Error while stopping readers multi handler.");
			}

			_bus.Publish(new SystemMessage.ServiceShutdown("StorageReader"));
		}

		void IHandle<SystemMessage.BecomeShutdown>.Handle(SystemMessage.BecomeShutdown message) {
			// by now (in case of successful shutdown process), all readers and writers should not be using ReadIndex
			_readIndex.Close();
		}

		void IHandle<MonitoringMessage.InternalStatsRequest>.Handle(MonitoringMessage.InternalStatsRequest message) {
			var s = _readIndex.GetStatistics();
			var stats = new Dictionary<string, object> {
				{"es-readIndex-cachedRecord", s.CachedRecordReads},
				{"es-readIndex-notCachedRecord", s.NotCachedRecordReads},
				{"es-readIndex-cachedStreamInfo", s.CachedStreamInfoReads},
				{"es-readIndex-notCachedStreamInfo", s.NotCachedStreamInfoReads},
				{"es-readIndex-cachedTransInfo", s.CachedTransInfoReads},
				{"es-readIndex-notCachedTransInfo", s.NotCachedTransInfoReads},
			};

			message.Envelope.ReplyWith(new MonitoringMessage.InternalStatsRequestResponse(stats));
		}
	}
}
