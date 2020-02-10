using System;
using System.Net.Http;
using System.Threading.Tasks;
using Grpc.Core;
using Xunit;

namespace EventStore.Client.Streams {
	[Trait("Category", "Network")]
	public class stream_metadata_with_timeout : IClassFixture<stream_metadata_with_timeout.Fixture> {
		private readonly Fixture _fixture;

		public stream_metadata_with_timeout(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task set_with_any_stream_revision_fails_when_operation_expired() {
			var stream = _fixture.GetStreamName();
			var rpcException = await Assert.ThrowsAsync<RpcException>(() =>
				_fixture.Client.SetStreamMetadataAsync(stream, AnyStreamRevision.Any, new StreamMetadata(),
					options => options.TimeoutAfter = TimeSpan.Zero));
			
			Assert.Equal(StatusCode.DeadlineExceeded, rpcException.StatusCode);
		}

		[Fact]
		public async Task set_with_stream_revision_fails_when_operation_expired() {
			var stream = _fixture.GetStreamName();

			var rpcException = await Assert.ThrowsAsync<RpcException>(() =>
				_fixture.Client.SetStreamMetadataAsync(stream, new StreamRevision(0), new StreamMetadata(),
					options => options.TimeoutAfter = TimeSpan.Zero));
			
			Assert.Equal(StatusCode.DeadlineExceeded, rpcException.StatusCode);
		}

		[Fact]
		public async Task get_fails_when_operation_expired() {
			var stream = _fixture.GetStreamName();
			var rpcException = await Assert.ThrowsAsync<RpcException>(() =>
				_fixture.Client.GetStreamMetadataAsync(stream,
					options => options.TimeoutAfter = TimeSpan.Zero));
			
			Assert.Equal(StatusCode.DeadlineExceeded, rpcException.StatusCode);
		}

		public class Fixture : EventStoreGrpcFixture {
			public Fixture() : base(clientSettings: new EventStoreClientSettings(new UriBuilder().Uri) {
				CreateHttpClient = () =>
					new HttpClient(new ResponseVersionHandler {InnerHandler = new DelayedHandler(200)})
			}) {
			}
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
