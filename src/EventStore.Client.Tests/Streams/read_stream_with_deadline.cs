using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Streams {
	[Trait("Category", "Network")]
	public class read_stream_with_deadline : IClassFixture<read_stream_with_deadline.Fixture> {
		private readonly Fixture _fixture;

		public read_stream_with_deadline(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task fails_when_deadline_is_reached() {
			var stream = _fixture.GetStreamName();

			await Assert.ThrowsAsync<TimeoutException>(() => _fixture.Client
				.ReadStreamAsync(Direction.Backwards, stream, StreamRevision.Start, 1,
					timeoutAfter: TimeSpan.FromHours(-1))
				.ToArrayAsync().AsTask());
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
