using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Cluster;
using EventStore.Core.Messages;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Fakes;
using NUnit.Framework;

namespace EventStore.Core.Tests.Cluster {
	public class EventStoreClientCacheTests {
		private static readonly Func<IPEndPoint, IPublisher, EventStoreClusterClient> EventStoreClusterClientFactory =
			(endpoint, bus) =>
				new EventStoreClusterClient(
					new UriBuilder(Uri.UriSchemeHttps, endpoint.Address.ToString(), endpoint.Port).Uri, bus);

		[Test]
		public void BusShouldNotBeNull() {
			Assert.Throws<ArgumentNullException>(() =>
				new EventStoreClusterClientCache(null, EventStoreClusterClientFactory));
		}

		[Test]
		public void ClientFactoryShouldNotBeNull() {
			Assert.Throws<ArgumentNullException>(() =>
				new EventStoreClusterClientCache(new FakePublisher(), null));
		}

		[Test]
		public void CanGetClientForEndpoint() {
			var sut = new EventStoreClusterClientCache(new FakePublisher(), EventStoreClusterClientFactory);

			var client = sut.Get(new IPEndPoint(IPAddress.Loopback, 1113));

			Assert.AreEqual(client, sut.Get(new IPEndPoint(IPAddress.Loopback, 1113)));
		}

		[Test]
		public async Task CleansCacheOnThreshold() {
			var interval = TimeSpan.FromMinutes(30);
			var oldItemThreshold = TimeSpan.FromSeconds(1);
			var sut = new EventStoreClusterClientCache(new FakePublisher(), EventStoreClusterClientFactory, interval,
				oldItemThreshold);
			var oldClient = sut.Get(new IPEndPoint(IPAddress.Loopback, 1113));

			sut.Handle(new ClusterClientMessage.CleanCache());
			var newClient = sut.Get(new IPEndPoint(IPAddress.Loopback, 1113));
			Assert.AreEqual(oldClient, newClient);

			await Task.Delay(oldItemThreshold);

			sut.Handle(new ClusterClientMessage.CleanCache());
			newClient = sut.Get(new IPEndPoint(IPAddress.Loopback, 1113));
			Assert.AreNotEqual(oldClient, newClient);
		}

		[Test]
		public void ShouldScheduleCacheCleanOnTimer() {
			var interval = TimeSpan.FromMilliseconds(1);
			var bus = new FakePublisher();
			var sut = new EventStoreClusterClientCache(bus, EventStoreClusterClientFactory, interval, interval);

			sut.Handle(new SystemMessage.SystemInit());

			Assert.True(bus.Messages.OfType<TimerMessage.Schedule>().Count() == 1);
		}

		[Test]
		public async Task ShouldDisposeClientOnceEvictedFromCache() {
			var interval = TimeSpan.FromMinutes(30);
			var oldItemThreshold = TimeSpan.FromMilliseconds(200);
			var sut = new EventStoreClusterClientCache(new FakePublisher(), EventStoreClusterClientFactory, interval,
				oldItemThreshold);
			var client = sut.Get(new IPEndPoint(IPAddress.Loopback, 1113));
			Assert.NotNull(client);

			await Task.Delay(oldItemThreshold);
			sut.Handle(new ClusterClientMessage.CleanCache());
			// Give the cache enough time to dispose the client
			await Task.Delay(oldItemThreshold);

			Assert.True(client.Disposed);
		}
	}
}
