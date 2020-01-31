using System;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Streams {
	public class append_to_stream_with_deadline : IClassFixture<append_to_stream_with_deadline.Fixture> {
		private readonly Fixture _fixture;

		public append_to_stream_with_deadline(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task fails_when_deadline_is_reached() {
			var stream = _fixture.GetStreamName();

			await Assert.ThrowsAsync<TimeoutException>(() => _fixture.Client.AppendToStreamAsync(
				stream,
				AnyStreamRevision.NoStream,
				_fixture.CreateTestEvents(),
				timeoutAfter: TimeSpan.FromHours(-1)));
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
