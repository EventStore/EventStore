using System;
using System.Net.Http;
using System.Threading.Tasks;
using Grpc.Core;
using Xunit;

namespace EventStore.Client.Streams {
	[Trait("Category", "Network")]
	public class deleting_stream_with_timeout : IClassFixture<deleting_stream_with_timeout.Fixture> {
		private readonly Fixture _fixture;

		public deleting_stream_with_timeout(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task any_stream_revision_soft_delete_fails_when_operation_expired() {
			var stream = _fixture.GetStreamName();
			var rpcException = await Assert.ThrowsAsync<RpcException>(() =>
				_fixture.Client.SoftDeleteAsync(stream, AnyStreamRevision.Any,
					options => options.TimeoutAfter = TimeSpan.Zero));

			Assert.Equal(StatusCode.DeadlineExceeded, rpcException.StatusCode);
		}

		[Fact]
		public async Task stream_revision_soft_delete_fails_when_operation_expired() {
			var stream = _fixture.GetStreamName();

			var rpcException = await Assert.ThrowsAsync<RpcException>(() =>
				_fixture.Client.SoftDeleteAsync(stream, new StreamRevision(0),
					options => options.TimeoutAfter = TimeSpan.Zero));

			Assert.Equal(StatusCode.DeadlineExceeded, rpcException.StatusCode);
		}

		[Fact]
		public async Task any_stream_revision_tombstoning_fails_when_operation_expired() {
			var stream = _fixture.GetStreamName();
			var rpcException = await Assert.ThrowsAsync<RpcException>(() =>
				_fixture.Client.TombstoneAsync(stream, AnyStreamRevision.Any,
					options => options.TimeoutAfter = TimeSpan.Zero));

			Assert.Equal(StatusCode.DeadlineExceeded, rpcException.StatusCode);
		}

		[Fact]
		public async Task stream_revision_tombstoning_fails_when_operation_expired() {
			var stream = _fixture.GetStreamName();

			var rpcException = await Assert.ThrowsAsync<RpcException>(() =>
				_fixture.Client.TombstoneAsync(stream, new StreamRevision(0),
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
