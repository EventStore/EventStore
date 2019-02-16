using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.Tests.Fakes;

namespace EventStore.Core.Tests.Services.Transport.Http {
	public class HttpBootstrap {
		public static void Subscribe(IBus bus, HttpService service) {
			bus.Subscribe<SystemMessage.SystemInit>(service);
			bus.Subscribe<SystemMessage.BecomeShuttingDown>(service);
			bus.Subscribe<HttpMessage.PurgeTimedOutRequests>(service);
		}

		public static void Unsubscribe(IBus bus, HttpService service) {
			bus.Unsubscribe<SystemMessage.SystemInit>(service);
			bus.Unsubscribe<SystemMessage.BecomeShuttingDown>(service);
			bus.Unsubscribe<HttpMessage.PurgeTimedOutRequests>(service);
		}

		public static void RegisterPing(HttpService service) {
			service.SetupController(new PingController());
		}

		public static void RegisterStat(HttpService service) {
			service.SetupController(new StatController(new FakePublisher(), new FakePublisher()));
		}
	}
}
