using System;
using System.Threading.Tasks;
using Grpc.Core;
using Xunit;

namespace EventStore.Client.Streams {
	[Trait("Category", "LongRunning")]
	public class stream_metadata_with_deadline : IClassFixture<stream_metadata_with_deadline.Fixture> {
		private readonly Fixture _fixture;

		public stream_metadata_with_deadline(Fixture fixture) {
			_fixture = fixture;
		}
		
		[Fact]
		public async Task fails_getting_when_deadline_is_reached() {
			var stream = _fixture.GetStreamName();
			
			await Assert.ThrowsAsync<TimeoutException>(() =>
				_fixture.Client.GetStreamMetadataAsync(stream,
					timeoutAfter: TimeSpan.FromHours(-1)));
		}
		
		[Fact]
		public async Task fails_settings_when_deadline_is_reached() {
			var stream = _fixture.GetStreamName();
			
			await Assert.ThrowsAsync<TimeoutException>(() =>
				_fixture.Client.SetStreamMetadataAsync(stream, AnyStreamRevision.Any, new StreamMetadata(),
					timeoutAfter: TimeSpan.FromHours(-1)));
		}
	
		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
