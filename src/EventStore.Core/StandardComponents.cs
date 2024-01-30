using EventStore.Core.Bus;
using EventStore.Core.Metrics;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core {
	public class StandardComponents {
		private readonly TFChunkDbConfig _dbConfig;
		private readonly IQueuedHandler _mainQueue;
		private readonly ISubscriber _mainBus;
		private readonly TimerService _timerService;
		private readonly ITimeProvider _timeProvider;
		private readonly IHttpForwarder _httpForwarder;
		private readonly IHttpService[] _httpServices;
		private readonly IPublisher _networkSendService;
		private readonly QueueStatsManager _queueStatsManager;

		public StandardComponents(
			TFChunkDbConfig dbConfig,
			IQueuedHandler mainQueue,
			ISubscriber mainBus,
			TimerService timerService,
			ITimeProvider timeProvider,
			IHttpForwarder httpForwarder,
			IHttpService[] httpServices,
			IPublisher networkSendService,
			QueueStatsManager queueStatsManager,
			QueueTrackers trackers) {
			_dbConfig = dbConfig;
			_mainQueue = mainQueue;
			_mainBus = mainBus;
			_timerService = timerService;
			_timeProvider = timeProvider;
			_httpForwarder = httpForwarder;
			_httpServices = httpServices;
			_networkSendService = networkSendService;
			_queueStatsManager = queueStatsManager;
			QueueTrackers = trackers;
		}

		public TFChunkDbConfig DbConfig {
			get { return _dbConfig; }
		}

		public IQueuedHandler MainQueue {
			get { return _mainQueue; }
		}

		public ISubscriber MainBus {
			get { return _mainBus; }
		}

		public TimerService TimerService {
			get { return _timerService; }
		}

		public ITimeProvider TimeProvider {
			get { return _timeProvider; }
		}

		public IHttpForwarder HttpForwarder {
			get { return _httpForwarder; }
		}

		public IHttpService[] HttpServices {
			get { return _httpServices; }
		}

		public IPublisher NetworkSendService {
			get { return _networkSendService; }
		}

		public QueueStatsManager QueueStatsManager {
			get { return _queueStatsManager; }
		}

		public QueueTrackers QueueTrackers { get; private set; }
	}
}
