using System;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Streams {
	[Trait("Category", "Network")]
	public class deleting_stream_with_deadline : IClassFixture<deleting_stream_with_deadline.Fixture> {
		private readonly Fixture _fixture;

		public deleting_stream_with_deadline(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task fails_soft_deleting_when_deadline_is_reached() {
			var stream = _fixture.GetStreamName();

			await Assert.ThrowsAsync<TimeoutException>(
				() => _fixture.Client.SoftDeleteAsync(stream, AnyStreamRevision.NoStream,
					timeoutAfter: TimeSpan.FromHours(-1)));
		}
		
		[Fact]
		public async Task fails_tombstoning_when_deadline_is_reached() {
			var stream = _fixture.GetStreamName();

			await Assert.ThrowsAsync<TimeoutException>(
				() => _fixture.Client.TombstoneAsync(stream, AnyStreamRevision.NoStream,
					timeoutAfter: TimeSpan.FromHours(-1)));
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
