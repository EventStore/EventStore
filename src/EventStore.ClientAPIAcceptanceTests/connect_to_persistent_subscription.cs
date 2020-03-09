using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public class connect_to_persistent_subscription
		: EventStoreClientAPITest {
		private const string Group = nameof(connect_to_persistent_subscription);
		private readonly EventStoreClientAPIFixture _fixture;

		public connect_to_persistent_subscription(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task that_does_not_exist_throws(bool useSsl) {
			var streamName = $"{GetStreamName()}_{useSsl}";
			var connection = _fixture.Connections[useSsl];

			var ex = await Record.ExceptionAsync(() => connection.ConnectToPersistentSubscriptionAsync(
				streamName, Group,
				delegate { return Task.CompletedTask; })).WithTimeout();
			if (ex is AggregateException agg) {
				agg = agg.Flatten();
				ex = Assert.Single(agg.InnerExceptions);
			}

			Assert.IsType<AccessDeniedException>(ex);

		}
	}
}
