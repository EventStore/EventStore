using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.SystemData;
using Xunit;

// ReSharper disable CheckNamespace
namespace EventStore.ClientAPI.Tests.Bugs {
	// ReSharper restore CheckNamespace

	public class Issue_1125 : EventStoreClientAPITest {
		private readonly EventStoreClientAPIFixture _fixture;

		public Issue_1125(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		public static IEnumerable<object[]> TestCases() => from i in Enumerable.Range(0, 50)
			from sslType in SslTypes
			select new object[] {i, sslType};

		[Theory(Skip = "Fixed in EventStoreClient"), MemberData(nameof(TestCases))]
		public async Task persistent_subscription_delivers_all_events(int iteration, SslType sslType) {
			const int eventCount = 250;
			const int totalEvents = eventCount * 2;
			EventStorePersistentSubscriptionBase subscription = null;

			try {
				var userCredentials = new UserCredentials("admin", "changeit");

				int hitCount = 0;

				var streamName = $"stream_{iteration}_{sslType}";
				var subscriptionName = $"subscription_{iteration}_{sslType}";

				var completed = new AutoResetEvent(false);

				var connection = _fixture.Connections[sslType];

				for (var i = 0; i < eventCount; i++) {
					await connection.AppendToStreamAsync(streamName, ExpectedVersion.Any, _fixture.CreateTestEvents())
						.WithTimeout();
				}

				var builder = PersistentSubscriptionSettings.Create()
					.StartFromBeginning()
					.ResolveLinkTos();

				await connection.CreatePersistentSubscriptionAsync(streamName,
					subscriptionName,
					builder.Build(),
					userCredentials).WithTimeout();
				subscription = await connection.ConnectToPersistentSubscriptionAsync(streamName, subscriptionName,
					(_, resolvedEvent) => {
						var result = Interlocked.Increment(ref hitCount);
						_.Acknowledge(resolvedEvent);

						if (totalEvents == result) {
							completed.Set();
						}
					});

				for (var i = 0; i < eventCount; i++) {
					await connection.AppendToStreamAsync(streamName, ExpectedVersion.Any, _fixture.CreateTestEvents())
						.WithTimeout();
				}

				completed.WaitOne(TimeSpan.FromSeconds(30));
				Assert.Equal(totalEvents, hitCount);
			} finally {
				subscription?.Stop(TimeSpan.FromSeconds(5));
			}
		}
	}
}
