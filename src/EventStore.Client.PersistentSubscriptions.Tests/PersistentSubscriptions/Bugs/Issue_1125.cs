using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.PersistentSubscriptions.Bugs {
	public class Issue_1125 : IClassFixture<Issue_1125.Fixture> {
		private readonly Fixture _fixture;

		public Issue_1125(Fixture fixture) {
			_fixture = fixture;
		}

		public static IEnumerable<object[]> TestCases() => Enumerable.Range(0, 50)
			.Select(i => new object[] {i});

		[Theory, MemberData(nameof(TestCases))]
		public async Task persistent_subscription_delivers_all_events(int iteration) {
			const int eventCount = 250;
			const int totalEvents = eventCount * 2;

			var completed = new TaskCompletionSource<bool>();
			int hitCount = 0;

			var userCredentials = new UserCredentials("admin", "changeit");

			var streamName = $"stream_{iteration}";
			var subscriptionName = $"subscription_{iteration}";

			for (var i = 0; i < eventCount; i++) {
				await _fixture.Client.AppendToStreamAsync(streamName, AnyStreamRevision.Any,
					_fixture.CreateTestEvents());
			}

			await _fixture.Client.PersistentSubscriptions.CreateAsync(streamName, subscriptionName,
				new PersistentSubscriptionSettings(
					resolveLinkTos: true, startFrom: StreamRevision.Start),
				userCredentials: userCredentials);

			using (_fixture.Client.PersistentSubscriptions.Subscribe(streamName, subscriptionName,
				async (subscription, @event, arg3, arg4) => {
					var result = Interlocked.Increment(ref hitCount);
					await subscription.Ack(@event);

					if (totalEvents == result) {
						completed.TrySetResult(true);
					}
				})) {
				for (var i = 0; i < eventCount; i++) {
					await _fixture.Client.AppendToStreamAsync(streamName, AnyStreamRevision.Any,
						_fixture.CreateTestEvents());
				}

				await completed.Task.WithTimeout(TimeSpan.FromSeconds(30));
			}

			Assert.Equal(totalEvents, hitCount);
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
