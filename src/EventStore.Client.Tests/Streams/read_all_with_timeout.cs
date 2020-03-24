using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Streams {
	[Trait("Category", "Network")]
	public class read_all_with_timeout : IClassFixture<read_all_with_timeout.Fixture> {
		private readonly Fixture _fixture;

		public read_all_with_timeout(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task fails_when_operation_expired() {
			await Assert.ThrowsAsync<TimeoutException>(() => _fixture.Client
				.ReadAllAsync(Direction.Backwards, Position.Start, 1,
					options => options.TimeoutAfter = TimeSpan.Zero)
				.ToArrayAsync().AsTask());
		}

		public class Fixture : EventStoreGrpcFixture {
			public Fixture() : base(clientSettings: new EventStoreClientSettings {
				CreateHttpMessageHandler = () =>
					new ResponseVersionHandler {InnerHandler = new DelayedHandler(200)}
			}) {
			}

			protected override Task Given() => Task.CompletedTask;

			protected override Task When() => Task.CompletedTask;
		}
	}
}
