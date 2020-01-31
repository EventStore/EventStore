using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Streams {
	[Trait("Category", "LongRunning")]
	public class read_all_with_deadline : IClassFixture<read_all_with_deadline.Fixture> {
		private readonly Fixture _fixture;

		public read_all_with_deadline(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task fails_when_deadline_is_reached() {
			await Assert.ThrowsAsync<TimeoutException>(() => _fixture.Client
				.ReadAllAsync(Direction.Backwards, Position.Start, 1,
					timeoutAfter: TimeSpan.FromHours(-1))
				.ToArrayAsync().AsTask());
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;

			protected override Task When() => Task.CompletedTask;
		}
	}
}
