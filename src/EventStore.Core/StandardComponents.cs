using EventStore.Core.Bus;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core {
	public class StandardComponents {
		private readonly TFChunkDb _db;
		private readonly IQueuedHandler _mainQueue;
		private readonly ISubscriber _mainBus;
		private readonly TimerService _timerService;
		private readonly ITimeProvider _timeProvider;
		private readonly IHttpForwarder _httpForwarder;
		private readonly HttpService[] _httpServices;
		private readonly IPublisher _networkSendService;

		public StandardComponents(
			TFChunkDb db,
			IQueuedHandler mainQueue,
			ISubscriber mainBus,
			TimerService timerService,
			ITimeProvider timeProvider,
			IHttpForwarder httpForwarder,
			HttpService[] httpServices,
			IPublisher networkSendService) {
			_db = db;
			_mainQueue = mainQueue;
			_mainBus = mainBus;
			_timerService = timerService;
			_timeProvider = timeProvider;
			_httpForwarder = httpForwarder;
			_httpServices = httpServices;
			_networkSendService = networkSendService;
		}

		public TFChunkDb Db {
			get { return _db; }
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

		public HttpService[] HttpServices {
			get { return _httpServices; }
		}

		public IPublisher NetworkSendService {
			get { return _networkSendService; }
		}
	}
}
